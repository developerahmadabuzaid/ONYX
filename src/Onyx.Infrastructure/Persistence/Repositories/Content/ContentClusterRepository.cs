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

internal sealed class ContentClusterRepository(ApplicationDbContext dbContext) : IContentClusterRepository
{
    public Task<ContentCluster?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ContentClusters.Include(cluster => cluster.Members).SingleOrDefaultAsync(cluster => cluster.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ContentCluster>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.ContentClusters.AsNoTracking().Include(cluster => cluster.Members).OrderBy(cluster => cluster.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ContentCluster>> ListContainingAsync(Guid contentItemId, CancellationToken cancellationToken) =>
        await dbContext.ContentClusters.AsNoTracking().Include(cluster => cluster.Members)
            .Where(cluster => cluster.Members.Any(member => member.ContentItemId == contentItemId)).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(ContentCluster cluster) => dbContext.ContentClusters.Add(cluster);

    public void Remove(ContentCluster cluster) => dbContext.ContentClusters.Remove(cluster);

    public void SetExpectedVersion(ContentCluster cluster, uint version) => dbContext.Entry(cluster).Property(entry => entry.Version).OriginalValue = version;
}
