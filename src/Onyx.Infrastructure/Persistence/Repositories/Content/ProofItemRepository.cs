using Microsoft.EntityFrameworkCore;
using Onyx.Application.Proof;
using Onyx.Domain.Proof;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class ProofItemRepository(ApplicationDbContext dbContext) : IProofItemRepository
{
    public Task<ProofItem?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ProofItems.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProofItem>> ListForContentAsync(Guid contentItemId, CancellationToken cancellationToken) =>
        await dbContext.ProofItems.AsNoTracking().Where(item => item.ContentItemId == contentItemId)
            .OrderBy(item => item.Kind).ThenBy(item => item.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ProofItem>> ListPublishedForContentAsync(Guid contentItemId, CancellationToken cancellationToken) =>
        await dbContext.ProofItems.AsNoTracking().Where(item => item.ContentItemId == contentItemId && item.Status == ProofItemStatus.Published)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.PublishedAtUtc).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(ProofItem item) => dbContext.ProofItems.Add(item);

    public void Remove(ProofItem item) => dbContext.ProofItems.Remove(item);

    public void SetExpectedVersion(ProofItem item, uint version) => dbContext.Entry(item).Property(entry => entry.Version).OriginalValue = version;
}
