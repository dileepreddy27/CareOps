using CareOps.Application.Dashboard;
using CareOps.Infrastructure.Auth;
using CareOps.Infrastructure.BackgroundJobs;

namespace CareOps.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dashboard").WithTags("Operations dashboard")
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.CredentialingSpecialist, AppRoles.Manager, AppRoles.Administrator));
        group.MapGet("/", (DashboardService service, CancellationToken ct) => service.GetAsync(ct));
        group.MapPost("/run-compliance-scan", (ComplianceMonitor monitor, CancellationToken ct) => monitor.RunOnceAsync(ct))
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.Administrator));
        return endpoints;
    }
}
