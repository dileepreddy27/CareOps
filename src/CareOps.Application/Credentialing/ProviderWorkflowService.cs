using CareOps.Application.Abstractions;
using CareOps.Domain.Credentialing;
using Microsoft.EntityFrameworkCore;

namespace CareOps.Application.Credentialing;

public sealed class ProviderWorkflowService(
    IAppDbContext db,
    IFileMetadataStorage fileStorage,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider)
{
    public async Task<PageResult<ProviderSummaryDto>> GetQueueAsync(QueueQuery query, CancellationToken cancellationToken)
    {
        var providers = db.ProviderProfiles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            providers = providers.Where(x =>
                x.FirstName.ToLower().Contains(search) ||
                x.LastName.ToLower().Contains(search) ||
                x.Npi.Contains(search));
        }

        if (query.Status is not null) providers = providers.Where(x => x.Status == query.Status);
        if (query.ReviewerId is not null) providers = providers.Where(x => x.AssignedReviewerId == query.ReviewerId);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = await providers.CountAsync(cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var expiringThreshold = today.AddDays(30);

        var items = await providers
            .OrderBy(x => x.SlaDueAt == null).ThenBy(x => x.SlaDueAt).ThenByDescending(x => x.UpdatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ProviderSummaryDto(
                x.Id,
                x.FirstName + " " + x.LastName,
                x.Npi,
                x.Specialty,
                x.Region,
                x.Status,
                x.AssignedReviewerId,
                x.SlaDueAt,
                x.Credentials.Count,
                x.Credentials.Count(c => c.ExpiresOn >= today && c.ExpiresOn <= expiringThreshold),
                x.ChecklistItems.Count(c => c.Result == VerificationResult.Passed),
                x.ChecklistItems.Count,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new(items, page, pageSize, total);
    }

    public async Task<ProviderDetailDto> GetAsync(Guid providerId, Guid requesterId, bool canViewAll, CancellationToken cancellationToken)
    {
        var provider = await LoadAsync(providerId, cancellationToken);
        if (!canViewAll && provider.UserId != requesterId) throw new UnauthorizedAccessException("Providers may only view their own record.");
        return Map(provider, includeInternal: canViewAll);
    }

    public async Task<ProviderDetailDto> GetMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var provider = await db.ProviderProfiles
            .AsNoTracking()
            .Include(x => x.Credentials).Include(x => x.ChecklistItems)
            .Include(x => x.Comments).Include(x => x.AuditEvents)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("No provider profile is linked to this user.");
        return Map(provider, includeInternal: false);
    }

    public async Task<ProviderProfile> CreateAsync(Guid userId, string npi, string firstName, string lastName, string specialty, string region, CancellationToken cancellationToken)
    {
        if (await db.ProviderProfiles.AnyAsync(x => x.UserId == userId || x.Npi == npi, cancellationToken))
            throw new InvalidOperationException("A profile already exists for this user or NPI.");

        var provider = new ProviderProfile(userId, npi, firstName, lastName, specialty, region, timeProvider.GetUtcNow());
        db.ProviderProfiles.Add(provider);
        await db.SaveChangesAsync(cancellationToken);
        return provider;
    }

    public async Task UpdateProfileAsync(Guid providerId, UpdateProfileRequest request, Guid actorId, bool canEditAll, CancellationToken cancellationToken)
    {
        var provider = await LoadAsync(providerId, cancellationToken);
        EnsureOwnerOrOperations(provider, actorId, canEditAll);
        provider.UpdateContact(request.Specialty, request.Region, request.Phone, timeProvider.GetUtcNow());
        await SaveAndNotifyAsync(provider, cancellationToken);
    }

    public async Task AddCredentialAsync(Guid providerId, AddCredentialRequest request, Guid actorId, bool canEditAll, CancellationToken cancellationToken)
    {
        var provider = await LoadAsync(providerId, cancellationToken);
        EnsureOwnerOrOperations(provider, actorId, canEditAll);
        var now = timeProvider.GetUtcNow();
        var storageKey = fileStorage.CreateStorageKey(provider.Id, request.OriginalFileName);
        var document = new CredentialDocument(provider.Id, request.Type, request.OriginalFileName, storageKey, request.ContentType, request.SizeBytes, request.Sha256, request.IssuedOn, request.ExpiresOn, now);
        provider.AddCredential(document, actorId, now);
        await SaveAndNotifyAsync(provider, cancellationToken);
    }

    public async Task SubmitAsync(Guid providerId, Guid actorId, bool canEditAll, CancellationToken cancellationToken)
    {
        var provider = await LoadAsync(providerId, cancellationToken);
        EnsureOwnerOrOperations(provider, actorId, canEditAll);
        provider.Submit(actorId, timeProvider.GetUtcNow());
        await SaveAndNotifyAsync(provider, cancellationToken);
    }

    public async Task AssignAsync(Guid providerId, Guid reviewerId, Guid actorId, CancellationToken cancellationToken)
    {
        var provider = await LoadAsync(providerId, cancellationToken);
        provider.AssignReviewer(reviewerId, actorId, timeProvider.GetUtcNow());
        await SaveAndNotifyAsync(provider, cancellationToken);
    }

    public async Task TransitionAsync(Guid providerId, TransitionRequest request, Guid actorId, bool canApprove, CancellationToken cancellationToken)
    {
        if (request.Status is (WorkflowStatus.Approved or WorkflowStatus.Suspended) && !canApprove)
            throw new UnauthorizedAccessException("Manager or administrator approval is required for this transition.");

        var provider = await LoadAsync(providerId, cancellationToken);
        provider.TransitionTo(request.Status, actorId, request.Reason, timeProvider.GetUtcNow());
        await SaveAndNotifyAsync(provider, cancellationToken);
    }

    public async Task ReviewCredentialAsync(Guid providerId, Guid credentialId, ReviewCredentialRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        var provider = await LoadAsync(providerId, cancellationToken);
        var credential = provider.Credentials.SingleOrDefault(x => x.Id == credentialId) ?? throw new KeyNotFoundException("Credential not found.");
        var now = timeProvider.GetUtcNow();
        if (request.Status == CredentialStatus.Verified) credential.Verify(actorId, now);
        else if (request.Status == CredentialStatus.Rejected) credential.Reject(actorId, now);
        else throw new InvalidOperationException("Review status must be Verified or Rejected.");
        provider.AddComment(actorId, $"Credential {credential.Type} marked {request.Status}.", false, now);
        await SaveAndNotifyAsync(provider, cancellationToken);
    }

    public async Task UpdateChecklistAsync(Guid providerId, Guid itemId, UpdateChecklistRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        var provider = await LoadAsync(providerId, cancellationToken);
        var item = provider.ChecklistItems.SingleOrDefault(x => x.Id == itemId) ?? throw new KeyNotFoundException("Checklist item not found.");
        item.Complete(request.Result, request.Evidence, actorId, timeProvider.GetUtcNow());
        await SaveAndNotifyAsync(provider, cancellationToken);
    }

    public async Task AddCommentAsync(Guid providerId, AddCommentRequest request, Guid actorId, bool canAddInternal, CancellationToken cancellationToken)
    {
        if (!request.VisibleToProvider && !canAddInternal) throw new UnauthorizedAccessException("Providers cannot add internal comments.");
        var provider = await LoadAsync(providerId, cancellationToken);
        EnsureOwnerOrOperations(provider, actorId, canAddInternal);
        provider.AddComment(actorId, request.Body, request.VisibleToProvider, timeProvider.GetUtcNow());
        await SaveAndNotifyAsync(provider, cancellationToken);
    }

    private async Task<ProviderProfile> LoadAsync(Guid providerId, CancellationToken cancellationToken) =>
        await db.ProviderProfiles
            .Include(x => x.Credentials).Include(x => x.ChecklistItems)
            .Include(x => x.Comments).Include(x => x.AuditEvents)
            .SingleOrDefaultAsync(x => x.Id == providerId, cancellationToken)
        ?? throw new KeyNotFoundException("Provider profile not found.");

    private async Task SaveAndNotifyAsync(ProviderProfile provider, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        await notifier.WorkflowChangedAsync(provider.Id, provider.Status.ToString(), cancellationToken);
    }

    private static void EnsureOwnerOrOperations(ProviderProfile provider, Guid actorId, bool canEditAll)
    {
        if (!canEditAll && provider.UserId != actorId) throw new UnauthorizedAccessException("Providers may only change their own record.");
    }

    private static ProviderDetailDto Map(ProviderProfile provider, bool includeInternal) => new(
        provider.Id,
        provider.UserId,
        provider.DisplayName,
        provider.Npi,
        provider.Specialty,
        provider.Region,
        provider.Phone,
        provider.Status,
        provider.AssignedReviewerId,
        provider.SubmittedAt,
        provider.SlaDueAt,
        provider.Credentials.OrderBy(x => x.ExpiresOn).Select(x => new CredentialDto(x.Id, x.Type, x.OriginalFileName, x.ContentType, x.SizeBytes, x.Sha256, x.IssuedOn, x.ExpiresOn, x.Status, x.VerifiedAt)).ToList(),
        provider.ChecklistItems.OrderBy(x => x.SortOrder).Select(x => new ChecklistItemDto(x.Id, x.Name, x.IsRequired, x.SortOrder, x.Result, x.Evidence, x.CompletedAt)).ToList(),
        provider.Comments.Where(x => includeInternal || x.VisibleToProvider).OrderByDescending(x => x.CreatedAt).Select(x => new CommentDto(x.Id, x.AuthorUserId, x.Body, x.VisibleToProvider, x.CreatedAt)).ToList(),
        provider.AuditEvents.Where(x => includeInternal || x.Action is "workflow.transitioned" or "credential.added").OrderByDescending(x => x.CreatedAt).Select(x => new AuditEventDto(x.Id, x.ActorUserId, x.Action, x.Details, x.CreatedAt)).ToList());
}
