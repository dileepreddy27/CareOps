using CareOps.Api.Middleware;
using CareOps.Application.Scheduling;
using CareOps.Infrastructure.Auth;

namespace CareOps.Api.Endpoints;

public static class SchedulingEndpoints
{
    public static IEndpointRouteBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/schedule").WithTags("Scheduling").RequireAuthorization();
        group.MapGet("/shifts", (HttpContext context, SchedulingService service, CancellationToken ct) => service.GetAsync(context.User.UserId(), context.User.IsOperations(), ct));
        group.MapPost("/shifts", (CreateShiftRequest request, SchedulingService service, CancellationToken ct) => service.CreateAsync(request, ct))
            .AddEndpointFilter<ValidationFilter<CreateShiftRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.Manager, AppRoles.Administrator));
        group.MapPost("/shifts/{shiftId:guid}/confirm", async (Guid shiftId, HttpContext context, SchedulingService service, CancellationToken ct) =>
        {
            await service.ConfirmAsync(shiftId, context.User.UserId(), ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole(AppRoles.Provider));
        return endpoints;
    }
}
