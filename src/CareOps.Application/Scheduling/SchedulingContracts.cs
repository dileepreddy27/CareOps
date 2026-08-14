using CareOps.Domain.Scheduling;
using FluentValidation;

namespace CareOps.Application.Scheduling;

public sealed record ShiftDto(Guid Id, Guid? ProviderProfileId, string Facility, string Department, DateTimeOffset StartsAt, DateTimeOffset EndsAt, ShiftStatus Status);
public sealed record CreateShiftRequest(string Facility, string Department, DateTimeOffset StartsAt, DateTimeOffset EndsAt, Guid? ProviderProfileId);

public sealed class CreateShiftRequestValidator : AbstractValidator<CreateShiftRequest>
{
    public CreateShiftRequestValidator()
    {
        RuleFor(x => x.Facility).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(150);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt);
    }
}
