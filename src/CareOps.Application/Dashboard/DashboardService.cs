using CareOps.Application.Abstractions;
using CareOps.Domain.Credentialing;
using Microsoft.EntityFrameworkCore;

namespace CareOps.Application.Dashboard;

public sealed record DashboardDto(
    int TotalProviders,
    int ActiveReviews,
    int SlaAtRisk,
    int ExpiringWithin30Days,
    decimal ComplianceRate,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyList<AlertDto> Alerts);

public sealed record AlertDto(Guid ProviderId, string ProviderName, string Severity, string Message, DateTimeOffset? DueAt);

public sealed class DashboardService(IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var inThirtyDays = today.AddDays(30);
        var providers = db.ProviderProfiles.AsNoTracking();
        var metrics = await providers
            .GroupBy(_ => 1)
            .Select(group => new DashboardMetrics(
                group.Count(),
                group.Count(x => x.Status == WorkflowStatus.Submitted || x.Status == WorkflowStatus.UnderReview || x.Status == WorkflowStatus.NeedsInformation),
                group.Count(x => x.SlaDueAt != null && x.SlaDueAt <= now.AddHours(12) && x.Status != WorkflowStatus.Approved),
                group.Count(x => x.Status == WorkflowStatus.Approved),
                group.Count(x => x.Status == WorkflowStatus.Draft),
                group.Count(x => x.Status == WorkflowStatus.Submitted),
                group.Count(x => x.Status == WorkflowStatus.UnderReview),
                group.Count(x => x.Status == WorkflowStatus.NeedsInformation),
                group.Count(x => x.Status == WorkflowStatus.Suspended),
                group.Count(x => x.Status == WorkflowStatus.Expired)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new DashboardMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var expiring = await db.CredentialDocuments.CountAsync(x => x.ExpiresOn >= today && x.ExpiresOn <= inThirtyDays, cancellationToken);

        var alerts = await providers
            .Where(x => (x.SlaDueAt != null && x.SlaDueAt <= now.AddHours(12)) || x.Credentials.Any(c => c.ExpiresOn <= inThirtyDays))
            .OrderBy(x => x.SlaDueAt)
            .Take(8)
            .Select(x => new AlertDto(
                x.Id,
                x.FirstName + " " + x.LastName,
                x.SlaDueAt < now ? "critical" : "warning",
                x.SlaDueAt < now ? "Credentialing SLA is overdue" : "Credential or SLA requires attention",
                x.SlaDueAt))
            .ToListAsync(cancellationToken);

        var byStatus = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [nameof(WorkflowStatus.Draft)] = metrics.Draft,
            [nameof(WorkflowStatus.Submitted)] = metrics.Submitted,
            [nameof(WorkflowStatus.UnderReview)] = metrics.UnderReview,
            [nameof(WorkflowStatus.NeedsInformation)] = metrics.NeedsInformation,
            [nameof(WorkflowStatus.Approved)] = metrics.Approved,
            [nameof(WorkflowStatus.Suspended)] = metrics.Suspended,
            [nameof(WorkflowStatus.Expired)] = metrics.Expired,
        };

        return new(
            metrics.Total,
            metrics.ActiveReviews,
            metrics.SlaAtRisk,
            expiring,
            metrics.Total == 0 ? 0 : Math.Round(metrics.Approved * 100m / metrics.Total, 1),
            byStatus,
            alerts);
    }

    private sealed record DashboardMetrics(
        int Total,
        int ActiveReviews,
        int SlaAtRisk,
        int Approved,
        int Draft,
        int Submitted,
        int UnderReview,
        int NeedsInformation,
        int Suspended,
        int Expired);
}
