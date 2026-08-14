using CareOps.Domain.Credentialing;
using FluentValidation;

namespace CareOps.Application.Credentialing;

public sealed record QueueQuery(string? Search, WorkflowStatus? Status, Guid? ReviewerId, int Page = 1, int PageSize = 25);
public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record ProviderSummaryDto(
    Guid Id,
    string DisplayName,
    string Npi,
    string Specialty,
    string Region,
    WorkflowStatus Status,
    Guid? AssignedReviewerId,
    DateTimeOffset? SlaDueAt,
    int CredentialCount,
    int ExpiringCredentialCount,
    int ChecklistCompleted,
    int ChecklistTotal,
    DateTimeOffset UpdatedAt);

public sealed record ProviderDetailDto(
    Guid Id,
    Guid UserId,
    string DisplayName,
    string Npi,
    string Specialty,
    string Region,
    string? Phone,
    WorkflowStatus Status,
    Guid? AssignedReviewerId,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? SlaDueAt,
    IReadOnlyList<CredentialDto> Credentials,
    IReadOnlyList<ChecklistItemDto> Checklist,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<AuditEventDto> AuditHistory);

public sealed record CredentialDto(
    Guid Id,
    string Type,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DateOnly IssuedOn,
    DateOnly ExpiresOn,
    CredentialStatus Status,
    DateTimeOffset? VerifiedAt);

public sealed record ChecklistItemDto(Guid Id, string Name, bool IsRequired, int SortOrder, VerificationResult Result, string? Evidence, DateTimeOffset? CompletedAt);
public sealed record CommentDto(Guid Id, Guid AuthorUserId, string Body, bool VisibleToProvider, DateTimeOffset CreatedAt);
public sealed record AuditEventDto(Guid Id, Guid? ActorUserId, string Action, string Details, DateTimeOffset CreatedAt);

public sealed record UpdateProfileRequest(string Specialty, string Region, string? Phone);
public sealed record AddCredentialRequest(string Type, string OriginalFileName, string ContentType, long SizeBytes, string Sha256, DateOnly IssuedOn, DateOnly ExpiresOn);
public sealed record AssignReviewerRequest(Guid ReviewerId);
public sealed record TransitionRequest(WorkflowStatus Status, string? Reason);
public sealed record ReviewCredentialRequest(CredentialStatus Status);
public sealed record UpdateChecklistRequest(VerificationResult Result, string? Evidence);
public sealed record AddCommentRequest(string Body, bool VisibleToProvider);

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Specialty).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Region).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(30);
    }
}

public sealed class AddCredentialRequestValidator : AbstractValidator<AddCredentialRequest>
{
    private static readonly string[] AllowedContentTypes = ["application/pdf", "image/png", "image/jpeg"];

    public AddCredentialRequestValidator()
    {
        RuleFor(x => x.Type).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OriginalFileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).Must(AllowedContentTypes.Contains).WithMessage("Only PDF, PNG, and JPEG metadata is accepted.");
        RuleFor(x => x.SizeBytes).InclusiveBetween(1, 25 * 1024 * 1024);
        RuleFor(x => x.Sha256).Matches("^[a-fA-F0-9]{64}$");
        RuleFor(x => x.ExpiresOn).GreaterThan(x => x.IssuedOn);
    }
}

public sealed class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(2_000);
}
