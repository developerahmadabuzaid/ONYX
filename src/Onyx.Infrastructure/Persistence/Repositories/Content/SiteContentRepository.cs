using Microsoft.EntityFrameworkCore;
using Onyx.Application.SiteContent;
using Onyx.Domain.SiteContent;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class SiteContentRepository(ApplicationDbContext dbContext) : ISiteContentRepository
{
    public Task<SiteContentDocument?> GetAsync(string key, CancellationToken cancellationToken) =>
        dbContext.SiteContentDocuments.FirstOrDefaultAsync(document => document.Key == key, cancellationToken);

    public async Task<IReadOnlyList<SiteContentDocument>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.SiteContentDocuments.AsNoTracking().OrderBy(document => document.Key).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<string, string>> ListPublishedAsync(CancellationToken cancellationToken) =>
        await dbContext.SiteContentDocuments.AsNoTracking()
            .Where(document => document.PublishedJson != null)
            .Select(document => new { document.Key, document.PublishedJson })
            .ToDictionaryAsync(document => document.Key, document => document.PublishedJson!, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<SiteContentRevision>> ListRevisionsAsync(Guid documentId, int take, CancellationToken cancellationToken) =>
        await dbContext.SiteContentRevisions.AsNoTracking()
            .Where(revision => revision.DocumentId == documentId)
            .OrderByDescending(revision => revision.Number)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<SiteContentRevision?> GetRevisionAsync(Guid documentId, int number, CancellationToken cancellationToken) =>
        dbContext.SiteContentRevisions.AsNoTracking().FirstOrDefaultAsync(revision => revision.DocumentId == documentId && revision.Number == number, cancellationToken);

    public Task<bool> IsMediaReferencedAsync(Guid mediaAssetId, bool includeDrafts, CancellationToken cancellationToken) =>
        includeDrafts
            ? dbContext.SiteContentDocuments.AnyAsync(document => document.PublishedMediaIds.Contains(mediaAssetId) || document.DraftMediaIds.Contains(mediaAssetId), cancellationToken)
            : dbContext.SiteContentDocuments.AnyAsync(document => document.PublishedMediaIds.Contains(mediaAssetId), cancellationToken);

    public void Add(SiteContentDocument document) => dbContext.SiteContentDocuments.Add(document);

    public void AddRevision(SiteContentRevision revision) => dbContext.SiteContentRevisions.Add(revision);

    public void SetExpectedVersion(SiteContentDocument document, uint version) => dbContext.Entry(document).Property(entry => entry.Version).OriginalValue = version;
}
