using Microsoft.EntityFrameworkCore;
using Onyx.Application.Booking;
using Onyx.Contracts.Common;
using Onyx.Domain.Booking;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class BookingRepository(ApplicationDbContext dbContext) : IBookingRepository
{
    public Task<ConsultationBooking?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ConsultationBookings.SingleOrDefaultAsync(booking => booking.Id == id, cancellationToken);

    public Task<ConsultationBooking?> FindByManageTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.ConsultationBookings
            .Where(booking => booking.ManageTokenHash == tokenHash && booking.Status == BookingStatus.Confirmed)
            .OrderByDescending(booking => booking.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ConsultationBooking>> ListForLeadAsync(Guid leadId, CancellationToken cancellationToken) =>
        await dbContext.ConsultationBookings.AsNoTracking().Where(booking => booking.LeadId == leadId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<(DateTimeOffset StartUtc, DateTimeOffset EndUtc, Guid Id)>> ListBusyAsync(DateTimeOffset fromUtc, CancellationToken cancellationToken)
    {
        var busy = await dbContext.ConsultationBookings.AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Confirmed && booking.EndUtc > fromUtc)
            .Select(booking => new { booking.StartUtc, booking.EndUtc, booking.Id })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return busy.Select(booking => (booking.StartUtc, booking.EndUtc, booking.Id)).ToList();
    }

    public async Task<PagedResult<ConsultationBooking>> ListAsync(
        bool upcomingOnly, BookingStatus? status, DateTimeOffset now, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.ConsultationBookings.AsNoTracking();
        if (upcomingOnly)
        {
            query = query.Where(booking => booking.Status == BookingStatus.Confirmed && booking.EndUtc > now);
        }
        else if (status is { } wanted)
        {
            query = query.Where(booking => booking.Status == wanted);
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var ordered = upcomingOnly ? query.OrderBy(booking => booking.StartUtc) : query.OrderByDescending(booking => booking.StartUtc);
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<ConsultationBooking>(items, page, pageSize, total);
    }

    public void Add(ConsultationBooking booking) => dbContext.ConsultationBookings.Add(booking);

    public void SetExpectedVersion(ConsultationBooking booking, uint version) =>
        dbContext.Entry(booking).Property(entry => entry.Version).OriginalValue = version;
}
