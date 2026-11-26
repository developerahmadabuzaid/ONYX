using Microsoft.EntityFrameworkCore;
using Onyx.Application.Delivery;
using Onyx.Domain.Delivery;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class DeliveryProjectRepository(ApplicationDbContext dbContext) : IDeliveryProjectRepository
{
    public Task<DeliveryProject?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.DeliveryProjects
            .Include(project => project.Statuses)
            .Include(project => project.Transitions)
            .Include(project => project.Members)
            .AsSplitQuery()
            .SingleOrDefaultAsync(project => project.Id == id, cancellationToken);

    public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken) =>
        dbContext.DeliveryProjects.AnyAsync(project => project.Key == key, cancellationToken);

    public void Add(DeliveryProject project) => dbContext.DeliveryProjects.Add(project);

    public void SetExpectedVersion(DeliveryProject project, uint version) =>
        dbContext.Entry(project).Property(entry => entry.Version).OriginalValue = version;
}
