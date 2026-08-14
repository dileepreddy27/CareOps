using CareOps.Application.Abstractions;
using CareOps.Domain.Credentialing;
using CareOps.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace CareOps.Application.Scheduling;

public sealed class SchedulingService(IAppDbContext db, IRealtimeNotifier notifier, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ShiftDto>> GetAsync(Guid userId, bool canViewAll, CancellationToken cancellationToken)
    {
        var query = db.CoverageShifts.AsNoTracking().Where(x => x.StartsAt >= timeProvider.GetUtcNow().AddDays(-1));
        if (!canViewAll)
        {
            var providerId = await db.ProviderProfiles.Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
            query = query.Where(x => x.ProviderProfileId == providerId || x.Status == ShiftStatus.Open);
        }

        return await query.OrderBy(x => x.StartsAt)
            .Select(x => new ShiftDto(x.Id, x.ProviderProfileId, x.Facility, x.Department, x.StartsAt, x.EndsAt, x.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<ShiftDto> CreateAsync(CreateShiftRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var shift = new CoverageShift(request.Facility, request.Department, request.StartsAt, request.EndsAt, now);
        if (request.ProviderProfileId is { } providerId)
        {
            var approved = await db.ProviderProfiles.AnyAsync(x => x.Id == providerId && x.Status == WorkflowStatus.Approved, cancellationToken);
            if (!approved) throw new InvalidOperationException("Only an approved provider may be offered a shift.");
            shift.OfferTo(providerId, now);
        }

        db.CoverageShifts.Add(shift);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.ShiftChangedAsync(shift.Id, shift.Status.ToString(), cancellationToken);
        return new(shift.Id, shift.ProviderProfileId, shift.Facility, shift.Department, shift.StartsAt, shift.EndsAt, shift.Status);
    }

    public async Task ConfirmAsync(Guid shiftId, Guid userId, CancellationToken cancellationToken)
    {
        var providerId = await db.ProviderProfiles.Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Provider profile not found.");
        var shift = await db.CoverageShifts.SingleOrDefaultAsync(x => x.Id == shiftId, cancellationToken)
            ?? throw new KeyNotFoundException("Shift not found.");
        shift.Confirm(providerId, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await notifier.ShiftChangedAsync(shift.Id, shift.Status.ToString(), cancellationToken);
    }
}
