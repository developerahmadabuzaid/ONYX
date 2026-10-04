using Microsoft.EntityFrameworkCore;
using Onyx.Application.Brand;
using Onyx.Application.Content;
using Onyx.Application.Email;
using Onyx.Application.Intelligence;
using Onyx.Application.Knowledge;
using Onyx.Application.Media;
using Onyx.Contracts.Common;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class ContentCalendarRepository(ApplicationDbContext dbContext) : IContentCalendarRepository
{
    public Task<ContentCalendarEntry?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ContentCalendarEntries.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);

    public async Task<PagedResult<ContentCalendarEntry>> ListAsync(CalendarEntryStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.ContentCalendarEntries.AsNoTracking();
        if (status is { } filter)
        {
            query = query.Where(entry => entry.Status == filter);
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(entry => entry.ProposedPublishAtUtc == null).ThenBy(entry => entry.ProposedPublishAtUtc).ThenByDescending(entry => entry.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<ContentCalendarEntry>(items, page, pageSize, total);
    }

    public void Add(ContentCalendarEntry entry) => dbContext.ContentCalendarEntries.Add(entry);

    public void SetExpectedVersion(ContentCalendarEntry entry, uint version) => dbContext.Entry(entry).Property(item => item.Version).OriginalValue = version;
}
