using FluentValidation;

namespace CareOps.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterProviderRequest(
    string Email,
    string Password,
    string Npi,
    string FirstName,
    string LastName,
    string Specialty,
    string Region);

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
public sealed record UserDto(Guid Id, string Email, IReadOnlyList<string> Roles, Guid? ProviderProfileId);

public interface IAccessTokenFactory
{
    AuthResponse Create(Guid userId, string email, IReadOnlyList<string> roles, Guid? providerProfileId);
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class RegisterProviderRequestValidator : AbstractValidator<RegisterProviderRequest>
{
    public RegisterProviderRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).MinimumLength(12).Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]");
        RuleFor(x => x.Npi).Matches("^[0-9]{10}$");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Specialty).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Region).NotEmpty().MaximumLength(100);
    }
}
