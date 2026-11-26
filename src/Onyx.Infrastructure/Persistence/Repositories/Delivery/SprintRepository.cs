using Microsoft.EntityFrameworkCore;
using Onyx.Application.Delivery;
using Onyx.Domain.Delivery;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class SprintRepository(ApplicationDbContext dbContext) : ISprintRepository
{
    public Task<Sprint?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.DeliverySprints.SingleOrDefaultAsync(sprint => sprint.Id == id, cancellationToken);

    public Task<Guid?> ActiveSprintIdAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.DeliverySprints
            .Where(sprint => sprint.ProjectId == projectId && sprint.State == SprintState.Active)
            .Select(sprint => (Guid?)sprint.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public void Add(Sprint sprint) => dbContext.DeliverySprints.Add(sprint);

    public void SetExpectedVersion(Sprint sprint, uint version) =>
        dbContext.Entry(sprint).Property(entry => entry.Version).OriginalValue = version;
}
