using CareOps.Api.Middleware;
using CareOps.Application.Auth;
using CareOps.Application.Credentialing;
using CareOps.Infrastructure.Auth;
using CareOps.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CareOps.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Authentication");
        group.MapPost("/login", LoginAsync).AddEndpointFilter<ValidationFilter<LoginRequest>>().AllowAnonymous();
        group.MapPost("/register/provider", RegisterProviderAsync).AddEndpointFilter<ValidationFilter<RegisterProviderRequest>>().AllowAnonymous();
        group.MapGet("/me", MeAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAccessTokenFactory tokens,
        CareOpsDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null) return Results.Unauthorized();
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return Results.Unauthorized();
        var roles = await userManager.GetRolesAsync(user);
        var providerId = await db.ProviderProfiles.Where(x => x.UserId == user.Id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        return Results.Ok(tokens.Create(user.Id, user.Email!, roles.ToArray(), providerId));
    }

    private static async Task<IResult> RegisterProviderAsync(
        RegisterProviderRequest request,
        UserManager<ApplicationUser> userManager,
        ProviderWorkflowService workflows,
        IAccessTokenFactory tokens,
        CareOpsDbContext db,
        CancellationToken cancellationToken)
    {
        IResult? validationFailure = null;
        AuthResponse? response = null;
        Guid profileId = default;
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var user = new ApplicationUser { UserName = request.Email, Email = request.Email, DisplayName = $"{request.FirstName} {request.LastName}" };
            var created = await userManager.CreateAsync(user, request.Password);
            if (!created.Succeeded)
            {
                validationFailure = Results.ValidationProblem(created.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(error => error.Description).ToArray()));
                return;
            }

            var addedRole = await userManager.AddToRoleAsync(user, AppRoles.Provider);
            if (!addedRole.Succeeded) throw new InvalidOperationException(string.Join("; ", addedRole.Errors.Select(x => x.Description)));
            var profile = await workflows.CreateAsync(user.Id, request.Npi, request.FirstName, request.LastName, request.Specialty, request.Region, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            profileId = profile.Id;
            response = tokens.Create(user.Id, request.Email, [AppRoles.Provider], profile.Id);
        });

        return validationFailure ?? Results.Created($"/api/providers/{profileId}", response!);
    }

    private static async Task<IResult> MeAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        CareOpsDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(context.User) ?? throw new UnauthorizedAccessException();
        var roles = await userManager.GetRolesAsync(user);
        var providerId = await db.ProviderProfiles.Where(x => x.UserId == user.Id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        return Results.Ok(new UserDto(user.Id, user.Email!, roles.ToArray(), providerId));
    }
}
