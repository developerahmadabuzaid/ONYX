using Microsoft.EntityFrameworkCore;
using Onyx.Application.Delivery;
using Onyx.Domain.Delivery;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class SavedIssueFilterRepository(ApplicationDbContext dbContext) : ISavedIssueFilterRepository
{
    public Task<SavedIssueFilter?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.DeliverySavedIssueFilters.SingleOrDefaultAsync(filter => filter.Id == id, cancellationToken);

    public Task<int> CountForOwnerAsync(Guid ownerId, CancellationToken cancellationToken) =>
        dbContext.DeliverySavedIssueFilters.CountAsync(filter => filter.OwnerId == ownerId, cancellationToken);

    public void Add(SavedIssueFilter filter) => dbContext.DeliverySavedIssueFilters.Add(filter);

    public void Remove(SavedIssueFilter filter) => dbContext.DeliverySavedIssueFilters.Remove(filter);
}
