using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CareOps.Infrastructure.BackgroundJobs;

public sealed class ComplianceMonitorWorker(IServiceScopeFactory scopeFactory, ILogger<ComplianceMonitorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ComplianceMonitor>().RunOnceAsync(stoppingToken);
                logger.LogInformation("Compliance scan completed: {Providers} providers, {Alerts} alerts, {Expirations} expirations", result.ProvidersScanned, result.AlertsCreated, result.ProfilesExpired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Compliance scan failed; the next scheduled scan will retry");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
