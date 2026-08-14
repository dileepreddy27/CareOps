using CareOps.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace CareOps.Infrastructure.Realtime;

public sealed class SignalRNotifier(IHubContext<WorkflowHub> hub) : IRealtimeNotifier
{
    public Task WorkflowChangedAsync(Guid providerId, string status, CancellationToken cancellationToken) =>
        hub.Clients.All.SendAsync("workflowChanged", new { providerId, status, occurredAt = DateTimeOffset.UtcNow }, cancellationToken);

    public Task NotificationRaisedAsync(Guid? recipientUserId, string title, CancellationToken cancellationToken) =>
        recipientUserId is { } id
            ? hub.Clients.Group($"user:{id}").SendAsync("notificationRaised", new { title }, cancellationToken)
            : hub.Clients.All.SendAsync("notificationRaised", new { title }, cancellationToken);

    public Task ShiftChangedAsync(Guid shiftId, string status, CancellationToken cancellationToken) =>
        hub.Clients.All.SendAsync("shiftChanged", new { shiftId, status, occurredAt = DateTimeOffset.UtcNow }, cancellationToken);
}
