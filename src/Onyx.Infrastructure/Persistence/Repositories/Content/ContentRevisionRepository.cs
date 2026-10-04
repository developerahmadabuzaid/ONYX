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

internal sealed class ContentRevisionRepository(ApplicationDbContext dbContext) : IContentRevisionRepository
{
    public void Add(ContentRevision revision) => dbContext.ContentRevisions.Add(revision);

    public async Task<ContentRevision?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ContentRevisions.Local.FirstOrDefault(revision => revision.Id == id)
        ?? await dbContext.ContentRevisions.AsNoTracking().SingleOrDefaultAsync(revision => revision.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, ContentRevision>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, ContentRevision>();
        }

        var result = dbContext.ContentRevisions.Local.Where(revision => ids.Contains(revision.Id)).ToDictionary(revision => revision.Id);
        var missing = ids.Where(id => !result.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            foreach (var revision in await dbContext.ContentRevisions.AsNoTracking().Where(revision => missing.Contains(revision.Id)).ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                result[revision.Id] = revision;
            }
        }

        return result;
    }

    public Task<ContentRevision?> GetByNumberAsync(Guid contentItemId, int number, CancellationToken cancellationToken) =>
        dbContext.ContentRevisions.AsNoTracking().SingleOrDefaultAsync(revision => revision.ContentItemId == contentItemId && revision.Number == number, cancellationToken);

    public async Task<ContentRevision?> GetLatestAsync(Guid contentItemId, CancellationToken cancellationToken) =>
        dbContext.ContentRevisions.Local.Where(revision => revision.ContentItemId == contentItemId).MaxBy(revision => revision.Number)
        ?? await dbContext.ContentRevisions.AsNoTracking().Where(revision => revision.ContentItemId == contentItemId)
            .OrderByDescending(revision => revision.Number).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<RevisionSummary>> ListAsync(Guid contentItemId, int take, CancellationToken cancellationToken) =>
        await dbContext.ContentRevisions.AsNoTracking()
            .Where(revision => revision.ContentItemId == contentItemId)
            .OrderByDescending(revision => revision.Number)
            .Take(take)
            .Select(revision => new RevisionSummary(revision.Id, revision.Number, revision.Kind, revision.Note, revision.CreatedAtUtc, revision.CreatedById, revision.RestoredFromRevisionId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
