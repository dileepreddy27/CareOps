using CareOps.Application.Abstractions;
using CareOps.Domain.Credentialing;
using CareOps.Domain.Scheduling;
using CareOps.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CareOps.Infrastructure.Data;

public sealed class CareOpsDbContext(DbContextOptions<CareOpsDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<ProviderProfile> ProviderProfiles => Set<ProviderProfile>();
    public DbSet<CredentialDocument> CredentialDocuments => Set<CredentialDocument>();
    public DbSet<VerificationChecklistItem> VerificationChecklistItems => Set<VerificationChecklistItem>();
    public DbSet<ReviewComment> ReviewComments => Set<ReviewComment>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<CoverageShift> CoverageShifts => Set<CoverageShift>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CareOpsDbContext).Assembly);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(x => x.DisplayName).HasMaxLength(200);
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
    }
}
