using CareOps.Domain.Credentialing;
using CareOps.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace CareOps.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<ProviderProfile> ProviderProfiles { get; }
    DbSet<CredentialDocument> CredentialDocuments { get; }
    DbSet<VerificationChecklistItem> VerificationChecklistItems { get; }
    DbSet<ReviewComment> ReviewComments { get; }
    DbSet<AuditEvent> AuditEvents { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<CoverageShift> CoverageShifts { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
