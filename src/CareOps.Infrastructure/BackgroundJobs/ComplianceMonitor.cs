using CareOps.Application.Abstractions;
using CareOps.Domain.Credentialing;
using Microsoft.EntityFrameworkCore;

namespace CareOps.Infrastructure.BackgroundJobs;

public sealed class ComplianceMonitor(IAppDbContext db, IRealtimeNotifier notifier, TimeProvider timeProvider)
{
    public async Task<ComplianceRunResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var providers = await db.ProviderProfiles.Include(x => x.Credentials).ToListAsync(cancellationToken);
        var existingKeys = await db.Notifications.Where(x => x.CreatedAt > now.AddDays(-35)).Select(x => x.DedupeKey).ToHashSetAsync(cancellationToken);
        var alerts = 0;
        var expirations = 0;

        foreach (var provider in providers)
        {
            var previousStatus = provider.Status;
            provider.ExpireIfRequired(today, now);
            if (provider.Status != previousStatus)
            {
                expirations++;
                await notifier.WorkflowChangedAsync(provider.Id, provider.Status.ToString(), cancellationToken);
            }

            foreach (var credential in provider.Credentials.Where(x => x.ExpiresWithin(today, 30)))
            {
                var days = credential.ExpiresOn.DayNumber - today.DayNumber;
                var bucket = days <= 7 ? 7 : 30;
                var key = $"credential-expiry:{credential.Id}:{bucket}:{credential.ExpiresOn:yyyyMMdd}";
                if (!existingKeys.Add(key)) continue;
                var notification = new Notification(provider.UserId, provider.Id, "credential.expiring", "Credential expiring", $"{credential.Type} expires in {days} day(s).", key, now);
                db.Notifications.Add(notification);
                alerts++;
                await notifier.NotificationRaisedAsync(provider.UserId, notification.Title, cancellationToken);
            }

            if (provider.SlaDueAt < now && provider.Status is not (WorkflowStatus.Approved or WorkflowStatus.Suspended or WorkflowStatus.Expired))
            {
                var key = $"sla-overdue:{provider.Id}:{now:yyyyMMdd}";
                if (!existingKeys.Add(key)) continue;
                var notification = new Notification(provider.AssignedReviewerId, provider.Id, "sla.overdue", "Credentialing SLA overdue", $"{provider.DisplayName}'s review passed its SLA target.", key, now);
                db.Notifications.Add(notification);
                alerts++;
                await notifier.NotificationRaisedAsync(provider.AssignedReviewerId, notification.Title, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new(providers.Count, alerts, expirations, now);
    }
}

public sealed record ComplianceRunResult(int ProvidersScanned, int AlertsCreated, int ProfilesExpired, DateTimeOffset CompletedAt);
