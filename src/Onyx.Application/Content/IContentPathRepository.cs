using Onyx.Domain.Content;

namespace Onyx.Application.Content;

public interface IContentPathRepository
{
    /// <summary>Tracked: every path the item has used.</summary>
    Task<IReadOnlyList<ContentPath>> ListForItemAsync(Guid contentItemId, CancellationToken cancellationToken);

    /// <summary>Read-only lookup of a path in one language.</summary>
    Task<ContentPath?> FindAsync(string languageCode, string path, CancellationToken cancellationToken);

    /// <summary>(item, language) → current path, for the given items.</summary>
    Task<IReadOnlyDictionary<(Guid ContentItemId, string LanguageCode), string>> GetCurrentPathsAsync(
        IReadOnlyCollection<Guid> contentItemIds, CancellationToken cancellationToken);

    void Add(ContentPath path);

    void Remove(ContentPath path);
}
