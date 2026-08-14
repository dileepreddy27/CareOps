using CareOps.Domain.Common;

namespace CareOps.Domain.Credentialing;

public sealed class AuditEvent : Entity
{
    private AuditEvent() { }

    internal AuditEvent(Guid providerProfileId, Guid? actorUserId, string action, string details, DateTimeOffset now)
    {
        ProviderProfileId = providerProfileId;
        ActorUserId = actorUserId;
        Action = action;
        Details = details;
        CreatedAt = UpdatedAt = now;
    }

    public Guid ProviderProfileId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
}
