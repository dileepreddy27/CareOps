using System.Text;
using CareOps.Application.Abstractions;
using CareOps.Application.Auth;
using CareOps.Infrastructure.Auth;
using CareOps.Infrastructure.BackgroundJobs;
using CareOps.Infrastructure.Data;
using CareOps.Infrastructure.Files;
using CareOps.Infrastructure.Realtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace CareOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("CareOps")
            ?? throw new InvalidOperationException("ConnectionStrings:CareOps is required.");
        services.AddDbContext<CareOpsDbContext>(options => options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3)));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<CareOpsDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddSignInManager()
        .AddEntityFrameworkStores<CareOpsDbContext>()
        .AddDefaultTokenProviders();

        var signingKey = configuration[$"{JwtOptions.SectionName}:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
                throw new InvalidOperationException("Jwt:SigningKey must be supplied by environment variable or secret provider outside Development.");
            signingKey = "careops-development-ephemeral-key-not-a-secret-2026";
        }

        var jwt = new JwtOptions
        {
            Issuer = configuration[$"{JwtOptions.SectionName}:Issuer"] ?? "CareOps.Api",
            Audience = configuration[$"{JwtOptions.SectionName}:Audience"] ?? "CareOps.Web",
            ExpirationMinutes = configuration.GetValue($"{JwtOptions.SectionName}:ExpirationMinutes", 60),
            SigningKey = signingKey
        };
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jwt));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var token = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs/workflow"))
                        context.Token = token;
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();
        services.AddSignalR();
        services.AddScoped<IAccessTokenFactory, JwtTokenFactory>();
        services.AddSingleton<IFileMetadataStorage, LocalFileMetadataStorage>();
        services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();
        services.AddScoped<ComplianceMonitor>();
        if (configuration.GetValue("BackgroundJobs:Enabled", true)) services.AddHostedService<ComplianceMonitorWorker>();
        return services;
    }
}
