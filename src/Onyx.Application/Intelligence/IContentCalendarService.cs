using Onyx.Contracts.Common;
using Onyx.Contracts.Intelligence;
using Onyx.Domain.Common;

namespace Onyx.Application.Intelligence;

public interface IContentCalendarService
{
    Task<Result<PagedResult<CalendarEntryResponse>>> ListAsync(string? status, int page, int pageSize, CancellationToken cancellationToken);

    Task<Result<CalendarEntryResponse>> ProposeAsync(ProposeCalendarEntryRequest request, CancellationToken cancellationToken);

    Task<Result<CalendarEntryResponse>> AcceptAsync(Guid id, DecideRequest request, CancellationToken cancellationToken);

    Task<Result<CalendarEntryResponse>> RejectAsync(Guid id, DecideRequest request, CancellationToken cancellationToken);

    Task<Result<CalendarEntryResponse>> LinkContentAsync(Guid id, LinkCalendarContentRequest request, CancellationToken cancellationToken);

    Task<Result<CalendarEntryResponse>> CompleteAsync(Guid id, DecideRequest request, CancellationToken cancellationToken);
}
