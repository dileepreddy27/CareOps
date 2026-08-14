using CareOps.Domain.Common;

namespace CareOps.Domain.Credentialing;

public sealed class ReviewComment : Entity
{
    private ReviewComment() { }

    internal ReviewComment(Guid providerProfileId, Guid authorUserId, string body, bool visibleToProvider, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 2_000)
            throw new DomainException("Comments must contain between 1 and 2,000 characters.");

        ProviderProfileId = providerProfileId;
        AuthorUserId = authorUserId;
        Body = body.Trim();
        VisibleToProvider = visibleToProvider;
        CreatedAt = UpdatedAt = now;
    }

    public Guid ProviderProfileId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public bool VisibleToProvider { get; private set; }
}
