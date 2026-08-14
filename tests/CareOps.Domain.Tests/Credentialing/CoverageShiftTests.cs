using CareOps.Domain.Common;
using CareOps.Domain.Scheduling;
using FluentAssertions;

namespace CareOps.Domain.Tests.Credentialing;

public sealed class CoverageShiftTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Shift_requires_a_positive_duration()
    {
        var act = () => new CoverageShift("Mercy General", "Emergency", Now.AddHours(8), Now.AddHours(8), Now);
        act.Should().Throw<DomainException>().WithMessage("*end*");
    }

    [Fact]
    public void Only_the_offered_provider_can_confirm_a_shift()
    {
        var offeredProvider = Guid.NewGuid();
        var shift = new CoverageShift("Mercy General", "Emergency", Now.AddDays(1), Now.AddDays(1).AddHours(8), Now);
        shift.OfferTo(offeredProvider, Now);

        var act = () => shift.Confirm(Guid.NewGuid(), Now.AddMinutes(5));

        act.Should().Throw<DomainException>();
        shift.Status.Should().Be(ShiftStatus.Offered);
    }

    [Fact]
    public void Offered_provider_can_confirm_a_shift()
    {
        var providerId = Guid.NewGuid();
        var shift = new CoverageShift("Mercy General", "Emergency", Now.AddDays(1), Now.AddDays(1).AddHours(8), Now);
        shift.OfferTo(providerId, Now);

        shift.Confirm(providerId, Now.AddMinutes(5));

        shift.Status.Should().Be(ShiftStatus.Confirmed);
    }
}
