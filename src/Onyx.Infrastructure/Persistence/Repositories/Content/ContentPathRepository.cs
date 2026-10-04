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

internal sealed class ContentPathRepository(ApplicationDbContext dbContext) : IContentPathRepository
{
    public async Task<IReadOnlyList<ContentPath>> ListForItemAsync(Guid contentItemId, CancellationToken cancellationToken)
    {
        var stored = await dbContext.ContentPaths.Where(path => path.ContentItemId == contentItemId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var added = dbContext.ContentPaths.Local.Where(path => path.ContentItemId == contentItemId && !stored.Contains(path));
        return stored.Concat(added).ToList();
    }

    public async Task<ContentPath?> FindAsync(string languageCode, string path, CancellationToken cancellationToken) =>
        dbContext.ContentPaths.Local.FirstOrDefault(entry => entry.LanguageCode == languageCode && entry.Path == path)
        ?? await dbContext.ContentPaths.AsNoTracking().SingleOrDefaultAsync(entry => entry.LanguageCode == languageCode && entry.Path == path, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<(Guid ContentItemId, string LanguageCode), string>> GetCurrentPathsAsync(
        IReadOnlyCollection<Guid> contentItemIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(Guid, string), string>();
        if (contentItemIds.Count == 0)
        {
            return result;
        }

        foreach (var path in await dbContext.ContentPaths.AsNoTracking()
                     .Where(path => contentItemIds.Contains(path.ContentItemId) && path.IsCurrent).ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            result[(path.ContentItemId, path.LanguageCode)] = path.Path;
        }

        // Overlay changes not yet saved in this unit of work (a path just retired or made current).
        foreach (var path in dbContext.ContentPaths.Local.Where(path => contentItemIds.Contains(path.ContentItemId)))
        {
            var key = (path.ContentItemId, path.LanguageCode);
            if (path.IsCurrent)
            {
                result[key] = path.Path;
            }
            else if (result.TryGetValue(key, out var current) && current == path.Path)
            {
                result.Remove(key);
            }
        }

        return result;
    }

    public void Add(ContentPath path) => dbContext.ContentPaths.Add(path);

    public void Remove(ContentPath path) => dbContext.ContentPaths.Remove(path);
}
