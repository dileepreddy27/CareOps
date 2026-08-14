namespace CareOps.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = "CareOps.Api";
    public string Audience { get; init; } = "CareOps.Web";
    public string SigningKey { get; init; } = string.Empty;
    public int ExpirationMinutes { get; init; } = 60;
}
