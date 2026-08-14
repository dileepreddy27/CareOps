using CareOps.Domain.Credentialing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareOps.Infrastructure.Data.Configurations;

public sealed class ProviderProfileConfiguration : IEntityTypeConfiguration<ProviderProfile>
{
    public void Configure(EntityTypeBuilder<ProviderProfile> builder)
    {
        builder.ToTable("provider_profiles");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.Npi).IsUnique();
        builder.HasIndex(x => new { x.Status, x.SlaDueAt });
        builder.Property(x => x.Npi).HasMaxLength(10);
        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.Specialty).HasMaxLength(150);
        builder.Property(x => x.Region).HasMaxLength(100);
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.DisplayName);

        builder.HasMany(x => x.Credentials).WithOne().HasForeignKey(x => x.ProviderProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.ChecklistItems).WithOne().HasForeignKey(x => x.ProviderProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Comments).WithOne().HasForeignKey(x => x.ProviderProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.AuditEvents).WithOne().HasForeignKey(x => x.ProviderProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CredentialDocumentConfiguration : IEntityTypeConfiguration<CredentialDocument>
{
    public void Configure(EntityTypeBuilder<CredentialDocument> builder)
    {
        builder.ToTable("credential_documents");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => x.ExpiresOn);
        builder.Property(x => x.Type).HasMaxLength(100);
        builder.Property(x => x.OriginalFileName).HasMaxLength(255);
        builder.Property(x => x.StorageKey).HasMaxLength(500);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.Sha256).HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
    }
}

public sealed class ChecklistConfiguration : IEntityTypeConfiguration<VerificationChecklistItem>
{
    public void Configure(EntityTypeBuilder<VerificationChecklistItem> builder)
    {
        builder.ToTable("verification_checklist_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Evidence).HasMaxLength(1_000);
        builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(30);
    }
}

public sealed class ReviewCommentConfiguration : IEntityTypeConfiguration<ReviewComment>
{
    public void Configure(EntityTypeBuilder<ReviewComment> builder)
    {
        builder.ToTable("review_comments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Body).HasMaxLength(2_000);
    }
}

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ProviderProfileId, x.CreatedAt });
        builder.Property(x => x.Action).HasMaxLength(100);
        builder.Property(x => x.Details).HasMaxLength(2_000);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.DedupeKey).IsUnique();
        builder.HasIndex(x => new { x.RecipientUserId, x.ReadAt });
        builder.Property(x => x.Type).HasMaxLength(60);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Message).HasMaxLength(1_000);
        builder.Property(x => x.DedupeKey).HasMaxLength(300);
    }
}
