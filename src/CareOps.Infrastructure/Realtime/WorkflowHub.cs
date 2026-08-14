using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CareOps.Infrastructure.Realtime;

[Authorize]
public sealed class WorkflowHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.UserIdentifier is { } userId)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        await base.OnConnectedAsync();
    }
}
