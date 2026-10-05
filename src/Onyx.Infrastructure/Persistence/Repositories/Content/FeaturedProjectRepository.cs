using Microsoft.EntityFrameworkCore;
using Onyx.Application.Proof;
using Onyx.Domain.Proof;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class FeaturedProjectRepository(ApplicationDbContext dbContext) : IFeaturedProjectRepository
{
    public async Task<IReadOnlyList<FeaturedProject>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.FeaturedProjects.AsNoTracking().OrderBy(featured => featured.Slot).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<FeaturedProject?> FindForContentAsync(Guid contentItemId, CancellationToken cancellationToken) =>
        dbContext.FeaturedProjects.SingleOrDefaultAsync(featured => featured.ContentItemId == contentItemId, cancellationToken);

    public void Add(FeaturedProject featured) => dbContext.FeaturedProjects.Add(featured);

    public void Remove(FeaturedProject featured) => dbContext.FeaturedProjects.Remove(featured);
}
