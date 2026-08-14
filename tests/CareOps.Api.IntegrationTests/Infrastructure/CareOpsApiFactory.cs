using CareOps.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CareOps.Api.IntegrationTests.Infrastructure;

public sealed class CareOpsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("careops_tests")
        .WithUsername("careops")
        .WithPassword("careops_tests")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:CareOps", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-which-is-long-enough-2026");
        builder.UseSetting("BackgroundJobs:Enabled", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CareOpsDbContext>>();
            services.AddDbContext<CareOpsDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    Task IAsyncLifetime.InitializeAsync() => _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class CareOpsApiCollection : ICollectionFixture<CareOpsApiFactory>
{
    public const string Name = "CareOps API";
}
