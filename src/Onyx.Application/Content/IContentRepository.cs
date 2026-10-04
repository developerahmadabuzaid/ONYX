using Onyx.Contracts.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

public interface IContentRepository
{
    /// <summary>Tracked, with translations.</summary>
    Task<ContentItem?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Read-only, with translations.</summary>
    Task<IReadOnlyDictionary<Guid, ContentItem>> GetLookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    void Add(ContentItem item);

    void Remove(ContentItem item);

    /// <summary>Optimistic concurrency: saving fails if the row changed since the caller read <paramref name="version"/>.</summary>
    void SetExpectedVersion(ContentItem item, uint version);

    Task<PagedResult<ContentItem>> ListAsync(ContentListFilter filter, CancellationToken cancellationToken);

    Task<bool> SlugTakenAsync(string languageCode, string slugScope, string slug, Guid excludeItemId, CancellationToken cancellationToken);

    Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Read-only children (with translations).</summary>
    Task<IReadOnlyList<ContentItem>> ListChildrenAsync(Guid parentId, CancellationToken cancellationToken);

    /// <summary>Tracked items whose scheduled publication is due.</summary>
    Task<IReadOnlyList<Guid>> ListDueForPublishAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListDueForUnpublishAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);

    /// <summary>Read-only live items (no translations; their live content is in the live revision).</summary>
    Task<IReadOnlyList<ContentItem>> ListLiveAsync(CancellationToken cancellationToken);

    /// <summary>Other items whose working copy uses the same primary keyword in this language.</summary>
    Task<IReadOnlyList<ContentTranslation>> FindByPrimaryKeywordAsync(string languageCode, string normalizedKeyword, Guid excludeItemId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContentTranslation>> FindByMetaTitleAsync(string languageCode, string metaTitle, Guid excludeItemId, CancellationToken cancellationToken);

    /// <summary>Another item sends its former URLs to this one (its offline redirect target).</summary>
    Task<bool> IsOfflineRedirectTargetAsync(Guid id, CancellationToken cancellationToken);
}
