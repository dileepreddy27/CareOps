namespace CareOps.Application.Abstractions;

public interface IRealtimeNotifier
{
    Task WorkflowChangedAsync(Guid providerId, string status, CancellationToken cancellationToken);
    Task NotificationRaisedAsync(Guid? recipientUserId, string title, CancellationToken cancellationToken);
    Task ShiftChangedAsync(Guid shiftId, string status, CancellationToken cancellationToken);
}
