using CareOps.Domain.Common;

namespace CareOps.Domain.Credentialing;

public sealed class Notification : Entity
{
    private Notification() { }

    public Notification(Guid? recipientUserId, Guid? providerProfileId, string type, string title, string message, string dedupeKey, DateTimeOffset now)
    {
        RecipientUserId = recipientUserId;
        ProviderProfileId = providerProfileId;
        Type = type;
        Title = title;
        Message = message;
        DedupeKey = dedupeKey;
        CreatedAt = UpdatedAt = now;
    }

    public Guid? RecipientUserId { get; private set; }
    public Guid? ProviderProfileId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string DedupeKey { get; private set; } = string.Empty;
    public DateTimeOffset? ReadAt { get; private set; }

    public void MarkRead(DateTimeOffset now)
    {
        ReadAt ??= now;
        Touch(now);
    }
}
