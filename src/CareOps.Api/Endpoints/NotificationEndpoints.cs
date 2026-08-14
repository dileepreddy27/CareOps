using CareOps.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CareOps.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();
        group.MapGet("/", async (HttpContext context, IAppDbContext db, CancellationToken ct) =>
        {
            var userId = context.User.UserId();
            return await db.Notifications.AsNoTracking()
                .Where(x => x.RecipientUserId == null || x.RecipientUserId == userId)
                .OrderByDescending(x => x.CreatedAt).Take(50)
                .Select(x => new { x.Id, x.Type, x.Title, x.Message, x.ProviderProfileId, x.CreatedAt, x.ReadAt })
                .ToListAsync(ct);
        });
        group.MapPost("/{id:guid}/read", async (Guid id, HttpContext context, IAppDbContext db, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var userId = context.User.UserId();
            var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && (x.RecipientUserId == null || x.RecipientUserId == userId), ct)
                ?? throw new KeyNotFoundException("Notification not found.");
            notification.MarkRead(timeProvider.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        return endpoints;
    }
}
