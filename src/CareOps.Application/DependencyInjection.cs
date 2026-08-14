using CareOps.Application.Credentialing;
using CareOps.Application.Dashboard;
using CareOps.Application.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace CareOps.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ProviderWorkflowService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<SchedulingService>();
        return services;
    }
}
