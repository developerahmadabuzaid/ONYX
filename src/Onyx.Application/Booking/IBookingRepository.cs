using Onyx.Contracts.Common;
using Onyx.Domain.Booking;

namespace Onyx.Application.Booking;

public interface IBookingRepository
{
    Task<ConsultationBooking?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The confirmed booking holding this manage-token hash (only the current booking of a chain holds it).</summary>
    Task<ConsultationBooking?> FindByManageTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConsultationBooking>> ListForLeadAsync(Guid leadId, CancellationToken cancellationToken);

    /// <summary>Time ranges of confirmed bookings that end after <paramref name="fromUtc"/>, for slot calculation.</summary>
    Task<IReadOnlyList<(DateTimeOffset StartUtc, DateTimeOffset EndUtc, Guid Id)>> ListBusyAsync(DateTimeOffset fromUtc, CancellationToken cancellationToken);

    Task<PagedResult<ConsultationBooking>> ListAsync(bool upcomingOnly, BookingStatus? status, DateTimeOffset now, int page, int pageSize, CancellationToken cancellationToken);

    void Add(ConsultationBooking booking);

    void SetExpectedVersion(ConsultationBooking booking, uint version);
}
