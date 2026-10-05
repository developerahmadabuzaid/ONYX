using Onyx.Contracts.Booking;
using Onyx.Contracts.Common;
using Onyx.Domain.Common;

namespace Onyx.Application.Booking;

public interface IBookingAdministrationService
{
    Task<Result<PagedResult<BookingResponse>>> ListAsync(BookingListQuery query, CancellationToken cancellationToken);

    Task<Result<BookingResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<BookingResponse>> CancelAsync(Guid id, CancelBookingRequest request, CancellationToken cancellationToken);

    Task<Result<BookingResponse>> RecordOutcomeAsync(Guid id, RecordBookingOutcomeRequest request, CancellationToken cancellationToken);
}
