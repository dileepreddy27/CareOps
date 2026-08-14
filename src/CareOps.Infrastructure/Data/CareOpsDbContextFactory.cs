using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CareOps.Infrastructure.Data;

public sealed class CareOpsDbContextFactory : IDesignTimeDbContextFactory<CareOpsDbContext>
{
    public CareOpsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CareOps")
            ?? "Host=localhost;Port=5432;Database=careops;Username=careops;Password=careops_dev";
        var options = new DbContextOptionsBuilder<CareOpsDbContext>().UseNpgsql(connectionString).Options;
        return new(options);
    }
}
