using Onyx.Contracts.Common;
using Onyx.Domain.Intelligence;

namespace Onyx.Application.Intelligence;

public interface IContentCalendarRepository
{
    Task<ContentCalendarEntry?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ContentCalendarEntry>> ListAsync(CalendarEntryStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    void Add(ContentCalendarEntry entry);

    void SetExpectedVersion(ContentCalendarEntry entry, uint version);
}
