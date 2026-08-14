using CareOps.Domain.Credentialing;
using CareOps.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareOps.Infrastructure.Data.Configurations;

public sealed class CoverageShiftConfiguration : IEntityTypeConfiguration<CoverageShift>
{
    public void Configure(EntityTypeBuilder<CoverageShift> builder)
    {
        builder.ToTable("coverage_shifts");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.StartsAt, x.Status });
        builder.Property(x => x.Facility).HasMaxLength(150);
        builder.Property(x => x.Department).HasMaxLength(150);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.HasOne<ProviderProfile>().WithMany().HasForeignKey(x => x.ProviderProfileId).OnDelete(DeleteBehavior.SetNull);
    }
}
