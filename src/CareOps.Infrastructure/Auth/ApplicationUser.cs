using Microsoft.AspNetCore.Identity;

namespace CareOps.Infrastructure.Auth;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}

public static class AppRoles
{
    public const string Provider = "Provider";
    public const string CredentialingSpecialist = "CredentialingSpecialist";
    public const string Manager = "Manager";
    public const string Administrator = "Administrator";

    public static readonly string[] All = [Provider, CredentialingSpecialist, Manager, Administrator];
    public const string Operations = CredentialingSpecialist + "," + Manager + "," + Administrator;
    public const string Leadership = Manager + "," + Administrator;
}
