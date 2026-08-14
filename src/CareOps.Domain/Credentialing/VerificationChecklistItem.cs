using CareOps.Domain.Common;

namespace CareOps.Domain.Credentialing;

public sealed class VerificationChecklistItem : Entity
{
    private VerificationChecklistItem() { }

    public VerificationChecklistItem(Guid providerProfileId, string name, bool isRequired, int sortOrder, DateTimeOffset now)
    {
        ProviderProfileId = providerProfileId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new DomainException("Checklist item name is required.") : name.Trim();
        IsRequired = isRequired;
        SortOrder = sortOrder;
        Result = VerificationResult.Pending;
        CreatedAt = UpdatedAt = now;
    }

    public Guid ProviderProfileId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsRequired { get; private set; }
    public int SortOrder { get; private set; }
    public VerificationResult Result { get; private set; }
    public string? Evidence { get; private set; }
    public Guid? CompletedByUserId { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void Complete(VerificationResult result, string? evidence, Guid actorId, DateTimeOffset now)
    {
        if (result == VerificationResult.Pending)
            throw new DomainException("A completed checklist item cannot remain pending.");

        Result = result;
        Evidence = string.IsNullOrWhiteSpace(evidence) ? null : evidence.Trim();
        CompletedByUserId = actorId;
        CompletedAt = now;
        Touch(now);
    }
}
