using System.Security.Claims;
using CareOps.Infrastructure.Auth;

namespace CareOps.Api.Endpoints;

internal static class EndpointAuthorization
{
    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("Authenticated user identifier is missing.");

    public static bool IsOperations(this ClaimsPrincipal principal) =>
        principal.IsInRole(AppRoles.CredentialingSpecialist) || principal.IsInRole(AppRoles.Manager) || principal.IsInRole(AppRoles.Administrator);

    public static bool IsLeadership(this ClaimsPrincipal principal) =>
        principal.IsInRole(AppRoles.Manager) || principal.IsInRole(AppRoles.Administrator);
}
