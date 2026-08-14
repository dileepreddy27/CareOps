using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CareOps.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CareOps.Infrastructure.Auth;

public sealed class JwtTokenFactory(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenFactory
{
    public AuthResponse Create(Guid userId, string email, IReadOnlyList<string> roles, Guid? providerProfileId)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        if (providerProfileId is { } id) claims.Add(new("provider_profile_id", id.ToString()));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now.UtcDateTime, expiresAt.UtcDateTime, credentials);

        return new(new JwtSecurityTokenHandler().WriteToken(token), expiresAt, new(userId, email, roles, providerProfileId));
    }
}
