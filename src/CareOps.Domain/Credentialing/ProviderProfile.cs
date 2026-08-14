using CareOps.Domain.Common;

namespace CareOps.Domain.Credentialing;

public sealed class ProviderProfile : Entity
{
    private static readonly IReadOnlyDictionary<WorkflowStatus, WorkflowStatus[]> AllowedTransitions =
        new Dictionary<WorkflowStatus, WorkflowStatus[]>
        {
            [WorkflowStatus.Draft] = [WorkflowStatus.Submitted],
            [WorkflowStatus.Submitted] = [WorkflowStatus.UnderReview, WorkflowStatus.NeedsInformation],
            [WorkflowStatus.UnderReview] = [WorkflowStatus.NeedsInformation, WorkflowStatus.Approved, WorkflowStatus.Suspended],
            [WorkflowStatus.NeedsInformation] = [WorkflowStatus.Submitted],
            [WorkflowStatus.Approved] = [WorkflowStatus.Suspended, WorkflowStatus.Expired],
            [WorkflowStatus.Suspended] = [WorkflowStatus.UnderReview, WorkflowStatus.Approved],
            [WorkflowStatus.Expired] = [WorkflowStatus.UnderReview]
        };

    private ProviderProfile() { }

    public ProviderProfile(Guid userId, string npi, string firstName, string lastName, string specialty, string region, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(npi) || npi.Trim().Length != 10 || !npi.All(char.IsDigit))
            throw new DomainException("NPI must contain exactly 10 digits.");

        UserId = userId;
        Npi = npi.Trim();
        FirstName = Required(firstName, nameof(firstName));
        LastName = Required(lastName, nameof(lastName));
        Specialty = Required(specialty, nameof(specialty));
        Region = Required(region, nameof(region));
        Status = WorkflowStatus.Draft;
        StatusChangedAt = now;
        CreatedAt = UpdatedAt = now;

        ChecklistItems =
        [
            new(Id, "Primary source license verification", true, 1, now),
            new(Id, "Sanctions and exclusions screening", true, 2, now),
            new(Id, "Education and training verification", true, 3, now),
            new(Id, "Professional liability coverage", true, 4, now)
        ];

        AuditEvents = [new(Id, userId, "profile.created", "Provider profile created.", now)];
    }

    public Guid UserId { get; private set; }
    public string Npi { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Specialty { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public WorkflowStatus Status { get; private set; }
    public DateTimeOffset StatusChangedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? SlaDueAt { get; private set; }
    public Guid? AssignedReviewerId { get; private set; }
    public List<CredentialDocument> Credentials { get; private set; } = [];
    public List<VerificationChecklistItem> ChecklistItems { get; private set; } = [];
    public List<ReviewComment> Comments { get; private set; } = [];
    public List<AuditEvent> AuditEvents { get; private set; } = [];

    public string DisplayName => $"{FirstName} {LastName}";

    public void UpdateContact(string specialty, string region, string? phone, DateTimeOffset now)
    {
        Specialty = Required(specialty, nameof(specialty));
        Region = Required(region, nameof(region));
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Touch(now);
    }

    public void AddCredential(CredentialDocument credential, Guid actorId, DateTimeOffset now)
    {
        if (credential.ProviderProfileId != Id) throw new DomainException("Credential belongs to a different provider.");
        Credentials.Add(credential);
        AuditEvents.Add(new(Id, actorId, "credential.added", $"Added {credential.Type} metadata.", now));
        Touch(now);
    }

    public void AssignReviewer(Guid reviewerId, Guid actorId, DateTimeOffset now)
    {
        AssignedReviewerId = reviewerId;
        AuditEvents.Add(new(Id, actorId, "review.assigned", $"Assigned reviewer {reviewerId}.", now));
        Touch(now);
    }

    public void AddComment(Guid authorId, string body, bool visibleToProvider, DateTimeOffset now)
    {
        Comments.Add(new(Id, authorId, body, visibleToProvider, now));
        AuditEvents.Add(new(Id, authorId, "comment.added", visibleToProvider ? "Provider-visible comment added." : "Internal comment added.", now));
        Touch(now);
    }

    public void Submit(Guid actorId, DateTimeOffset now)
    {
        if (Status is not (WorkflowStatus.Draft or WorkflowStatus.NeedsInformation))
            throw new DomainException($"A profile in {Status} cannot be submitted.");
        if (Credentials.Count == 0) throw new DomainException("At least one credential is required before submission.");

        ChangeStatus(WorkflowStatus.Submitted, actorId, "Submitted for credentialing review.", now);
        SubmittedAt = now;
        SlaDueAt = now.AddBusinessDays(3);
    }

    public void TransitionTo(WorkflowStatus next, Guid? actorId, string? reason, DateTimeOffset now)
    {
        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(next))
            throw new DomainException($"Transition from {Status} to {next} is not allowed.");

        if (next is (WorkflowStatus.NeedsInformation or WorkflowStatus.Suspended) && string.IsNullOrWhiteSpace(reason))
            throw new DomainException($"A reason is required when moving to {next}.");

        if (next == WorkflowStatus.Approved)
        {
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (Credentials.Count == 0 || Credentials.Any(x => x.Status != CredentialStatus.Verified || x.IsExpired(today)))
                throw new DomainException("All credentials must be current and verified before approval.");
            if (ChecklistItems.Any(x => x.IsRequired && x.Result != VerificationResult.Passed))
                throw new DomainException("All required checklist items must pass before approval.");
        }

        ChangeStatus(next, actorId, reason ?? $"Workflow moved to {next}.", now);
        SlaDueAt = next switch
        {
            WorkflowStatus.Submitted => now.AddBusinessDays(3),
            WorkflowStatus.UnderReview => now.AddBusinessDays(2),
            WorkflowStatus.NeedsInformation => now.AddBusinessDays(5),
            _ => null
        };
    }

    public void ExpireIfRequired(DateOnly today, DateTimeOffset now)
    {
        foreach (var credential in Credentials.Where(x => x.IsExpired(today) && x.Status != CredentialStatus.Expired))
            credential.MarkExpired(now);

        if (Status == WorkflowStatus.Approved && Credentials.Any(x => x.IsExpired(today)))
            ChangeStatus(WorkflowStatus.Expired, null, "Automatically expired because a credential elapsed.", now);
    }

    private void ChangeStatus(WorkflowStatus next, Guid? actorId, string details, DateTimeOffset now)
    {
        var previous = Status;
        Status = next;
        StatusChangedAt = now;
        AuditEvents.Add(new(Id, actorId, "workflow.transitioned", $"{previous} -> {next}. {details}", now));
        Touch(now);
    }

    private static string Required(string value, string field) =>
        string.IsNullOrWhiteSpace(value) ? throw new DomainException($"{field} is required.") : value.Trim();
}

internal static class BusinessDayExtensions
{
    public static DateTimeOffset AddBusinessDays(this DateTimeOffset value, int days)
    {
        var result = value;
        while (days > 0)
        {
            result = result.AddDays(1);
            if (result.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days--;
        }

        return result;
    }
}
