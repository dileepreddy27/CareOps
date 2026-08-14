using CareOps.Domain.Common;

namespace CareOps.Domain.Scheduling;

public enum ShiftStatus { Open, Offered, Confirmed, Cancelled }

public sealed class CoverageShift : Entity
{
    private CoverageShift() { }

    public CoverageShift(string facility, string department, DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(facility)) throw new DomainException("Facility is required.");
        if (string.IsNullOrWhiteSpace(department)) throw new DomainException("Department is required.");
        if (endsAt <= startsAt) throw new DomainException("Shift end must be after its start.");

        Facility = facility.Trim();
        Department = department.Trim();
        StartsAt = startsAt;
        EndsAt = endsAt;
        Status = ShiftStatus.Open;
        CreatedAt = UpdatedAt = now;
    }

    public Guid? ProviderProfileId { get; private set; }
    public string Facility { get; private set; } = string.Empty;
    public string Department { get; private set; } = string.Empty;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public ShiftStatus Status { get; private set; }

    public void OfferTo(Guid providerProfileId, DateTimeOffset now)
    {
        if (Status is not ShiftStatus.Open) throw new DomainException("Only an open shift can be offered.");
        ProviderProfileId = providerProfileId;
        Status = ShiftStatus.Offered;
        Touch(now);
    }

    public void Confirm(Guid providerProfileId, DateTimeOffset now)
    {
        if (Status != ShiftStatus.Offered || ProviderProfileId != providerProfileId)
            throw new DomainException("This shift is not offered to the provider.");
        Status = ShiftStatus.Confirmed;
        Touch(now);
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == ShiftStatus.Cancelled) return;
        Status = ShiftStatus.Cancelled;
        Touch(now);
    }
}
