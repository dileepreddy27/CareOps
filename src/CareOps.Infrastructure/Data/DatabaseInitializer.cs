using CareOps.Domain.Credentialing;
using CareOps.Domain.Scheduling;
using CareOps.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CareOps.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, IConfiguration configuration, IHostEnvironment environment, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareOpsDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AppRoles.All)
            if (!await roleManager.RoleExistsAsync(role))
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(role)));

        if (!environment.IsDevelopment() || !configuration.GetValue("Seed:Enabled", true)) return;

        var password = configuration["Seed:DemoPassword"] ?? "CareOps-Demo-2026!";
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var specialist = await EnsureUserAsync(userManager, "specialist@careops.local", "Avery Brooks", password, AppRoles.CredentialingSpecialist);
        var manager = await EnsureUserAsync(userManager, "manager@careops.local", "Morgan Ellis", password, AppRoles.Manager);
        await EnsureUserAsync(userManager, "admin@careops.local", "Casey Admin", password, AppRoles.Administrator);

        var providerSeeds = new[]
        {
            new ProviderSeed("maya.chen@careops.local", "Maya", "Chen", "1234567890", "Cardiology", "Northeast", WorkflowStatus.UnderReview, 12),
            new ProviderSeed("jordan.lee@careops.local", "Jordan", "Lee", "1234567891", "Emergency Medicine", "Midwest", WorkflowStatus.NeedsInformation, 75),
            new ProviderSeed("sam.rivera@careops.local", "Sam", "Rivera", "1234567892", "Family Medicine", "Southeast", WorkflowStatus.Approved, 22),
            new ProviderSeed("priya.patel@careops.local", "Priya", "Patel", "1234567893", "Pediatrics", "West", WorkflowStatus.Submitted, 140),
            new ProviderSeed("omar.hassan@careops.local", "Omar", "Hassan", "1234567894", "Radiology", "Northeast", WorkflowStatus.Draft, 210),
            new ProviderSeed("lena.morris@careops.local", "Lena", "Morris", "1234567895", "Anesthesiology", "Midwest", WorkflowStatus.Suspended, 180)
        };

        ProviderProfile? approvedProvider = null;
        foreach (var seed in providerSeeds)
        {
            var user = await EnsureUserAsync(userManager, seed.Email, $"{seed.FirstName} {seed.LastName}", password, AppRoles.Provider);
            if (await db.ProviderProfiles.AnyAsync(x => x.UserId == user.Id, cancellationToken)) continue;
            var profile = BuildProfile(seed, user.Id, specialist.Id, manager.Id, DateTimeOffset.UtcNow);
            db.ProviderProfiles.Add(profile);
            if (seed.Status == WorkflowStatus.Approved) approvedProvider = profile;
        }

        if (approvedProvider is not null)
        {
            var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(3).AddHours(8), TimeSpan.Zero);
            var shift = new CoverageShift("Mercy General", "Cardiac step-down", start, start.AddHours(8), DateTimeOffset.UtcNow);
            shift.OfferTo(approvedProvider.Id, DateTimeOffset.UtcNow);
            db.CoverageShifts.Add(shift);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static ProviderProfile BuildProfile(ProviderSeed seed, Guid userId, Guid specialistId, Guid managerId, DateTimeOffset now)
    {
        var profile = new ProviderProfile(userId, seed.Npi, seed.FirstName, seed.LastName, seed.Specialty, seed.Region, now.AddDays(-8));
        var credential = new CredentialDocument(profile.Id, "State medical license", $"{seed.LastName.ToLowerInvariant()}-license.pdf", $"providers/{profile.Id:N}/seed-license.pdf", "application/pdf", 248_000, new string('a', 64), DateOnly.FromDateTime(now.AddYears(-1).UtcDateTime), DateOnly.FromDateTime(now.AddDays(seed.ExpiresInDays).UtcDateTime), now.AddDays(-7));
        profile.AddCredential(credential, userId, now.AddDays(-7));
        if (seed.Status == WorkflowStatus.Draft) return profile;

        profile.Submit(userId, now.AddDays(-6));
        if (seed.Status == WorkflowStatus.Submitted) return profile;
        profile.AssignReviewer(specialistId, managerId, now.AddDays(-5));
        profile.TransitionTo(WorkflowStatus.UnderReview, specialistId, null, now.AddDays(-5));
        if (seed.Status == WorkflowStatus.UnderReview) return profile;
        if (seed.Status == WorkflowStatus.NeedsInformation)
        {
            profile.TransitionTo(WorkflowStatus.NeedsInformation, specialistId, "Please upload current malpractice coverage.", now.AddDays(-2));
            profile.AddComment(specialistId, "Malpractice policy declarations page is missing.", true, now.AddDays(-2));
            return profile;
        }

        credential.Verify(specialistId, now.AddDays(-4));
        foreach (var item in profile.ChecklistItems) item.Complete(VerificationResult.Passed, "Verified in demo primary source.", specialistId, now.AddDays(-3));
        profile.TransitionTo(WorkflowStatus.Approved, managerId, "Manager approval completed.", now.AddDays(-2));
        if (seed.Status == WorkflowStatus.Suspended)
            profile.TransitionTo(WorkflowStatus.Suspended, managerId, "Temporary quality review hold.", now.AddDays(-1));
        return profile;
    }

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string displayName, string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new() { UserName = email, Email = email, EmailConfirmed = true, DisplayName = displayName };
            EnsureSucceeded(await userManager.CreateAsync(user, password));
        }
        if (!await userManager.IsInRoleAsync(user, role)) EnsureSucceeded(await userManager.AddToRoleAsync(user, role));
        return user;
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private sealed record ProviderSeed(string Email, string FirstName, string LastName, string Npi, string Specialty, string Region, WorkflowStatus Status, int ExpiresInDays);
}
