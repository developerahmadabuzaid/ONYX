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

internal sealed class ContentRepository(ApplicationDbContext dbContext) : IContentRepository
{
    public Task<ContentItem?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ContentItems.Include(item => item.Translations).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, ContentItem>> GetLookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, ContentItem>();
        }

        // Tracked instances win, so changes made earlier in this unit of work (e.g. an item just published) are visible.
        var result = dbContext.ContentItems.Local.Where(item => ids.Contains(item.Id)).ToDictionary(item => item.Id);
        var missing = ids.Where(id => !result.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            foreach (var item in await dbContext.ContentItems.AsNoTracking().Include(item => item.Translations)
                         .Where(item => missing.Contains(item.Id)).ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                result[item.Id] = item;
            }
        }

        return result;
    }

    public void Add(ContentItem item) => dbContext.ContentItems.Add(item);

    public void Remove(ContentItem item) => dbContext.ContentItems.Remove(item);

    public void SetExpectedVersion(ContentItem item, uint version) => dbContext.Entry(item).Property(entry => entry.Version).OriginalValue = version;

    public async Task<PagedResult<ContentItem>> ListAsync(ContentListFilter filter, CancellationToken cancellationToken)
    {
        var allowed = filter.AllowedTypes.ToList();
        var query = dbContext.ContentItems.AsNoTracking().Where(item => allowed.Contains(item.Type));

        if (filter.Type is { } type)
        {
            query = query.Where(item => item.Type == type);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(item => item.Status == status);
        }

        if (filter.IsLive is { } isLive)
        {
            query = isLive
                ? query.Where(item => item.LiveRevisionId != null && item.Status != ContentStatus.Archived)
                : query.Where(item => item.LiveRevisionId == null || item.Status == ContentStatus.Archived);
        }

        if (filter.NeedsUrlDecision)
        {
            query = query.Where(item => item.OfflineResolution == OfflineResolution.PendingDecision);
        }

        if (filter.ParentId is { } parentId)
        {
            query = query.Where(item => item.ParentId == parentId);
        }

        if (filter.Search is { } search)
        {
            var pattern = LikePattern.Contains(search);
            query = query.Where(item => item.Translations.Any(translation => EF.Functions.ILike(translation.Title, pattern, LikePattern.EscapeCharacter)));
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Include(item => item.Translations)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedResult<ContentItem>(items, filter.Page, filter.PageSize, total);
    }

    public Task<bool> SlugTakenAsync(string languageCode, string slugScope, string slug, Guid excludeItemId, CancellationToken cancellationToken) =>
        dbContext.ContentTranslations.AnyAsync(
            translation => translation.LanguageCode == languageCode && translation.SlugScope == slugScope && translation.Slug == slug && translation.ContentItemId != excludeItemId,
            cancellationToken);

    public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ContentItems.AnyAsync(item => item.ParentId == id, cancellationToken);

    public async Task<IReadOnlyList<ContentItem>> ListChildrenAsync(Guid parentId, CancellationToken cancellationToken) =>
        await dbContext.ContentItems.AsNoTracking().Include(item => item.Translations).Where(item => item.ParentId == parentId)
            .OrderBy(item => item.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Guid>> ListDueForPublishAsync(DateTimeOffset now, int take, CancellationToken cancellationToken) =>
        await dbContext.ContentItems.AsNoTracking()
            .Where(item => item.Status == ContentStatus.Scheduled && item.ScheduledPublishAtUtc != null && item.ScheduledPublishAtUtc <= now)
            .OrderBy(item => item.ScheduledPublishAtUtc)
            .Select(item => item.Id)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Guid>> ListDueForUnpublishAsync(DateTimeOffset now, int take, CancellationToken cancellationToken) =>
        await dbContext.ContentItems.AsNoTracking()
            .Where(item => item.ScheduledUnpublishAtUtc != null && item.ScheduledUnpublishAtUtc <= now && item.LiveRevisionId != null)
            .OrderBy(item => item.ScheduledUnpublishAtUtc)
            .Select(item => item.Id)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ContentItem>> ListLiveAsync(CancellationToken cancellationToken) =>
        await dbContext.ContentItems.AsNoTracking().Include(item => item.Translations)
            .Where(item => item.LiveRevisionId != null && item.Status != ContentStatus.Archived)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ContentTranslation>> FindByPrimaryKeywordAsync(
        string languageCode, string normalizedKeyword, Guid excludeItemId, CancellationToken cancellationToken) =>
        await dbContext.ContentTranslations.AsNoTracking()
            .Where(translation => translation.LanguageCode == languageCode && translation.PrimaryKeywordNormalized == normalizedKeyword && translation.ContentItemId != excludeItemId)
            .Where(translation => dbContext.ContentItems.Any(item => item.Id == translation.ContentItemId && item.Status != ContentStatus.Archived))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ContentTranslation>> FindByMetaTitleAsync(string languageCode, string metaTitle, Guid excludeItemId, CancellationToken cancellationToken) =>
        await dbContext.ContentTranslations.AsNoTracking()
            .Where(translation => translation.LanguageCode == languageCode && translation.MetaTitle == metaTitle && translation.ContentItemId != excludeItemId)
            .Where(translation => dbContext.ContentItems.Any(item => item.Id == translation.ContentItemId && item.Status != ContentStatus.Archived))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<bool> IsOfflineRedirectTargetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ContentItems.AnyAsync(item => item.OfflineRedirectToId == id, cancellationToken);
}
