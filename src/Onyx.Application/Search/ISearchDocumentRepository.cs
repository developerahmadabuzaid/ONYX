namespace Onyx.Application.Search;

/// <summary>The search index (<c>search_documents</c>): one row per live public page per language.</summary>
public interface ISearchDocumentRepository
{
    /// <summary>Inserts or replaces the document for its page and language (idempotent).</summary>
    Task UpsertAsync(SearchDocumentData document, CancellationToken cancellationToken);

    Task<int> RemoveAsync(Guid contentItemId, string? languageCode, CancellationToken cancellationToken);

    /// <summary>Pages whose indexed path is one of <paramref name="paths"/> (site paths, unencoded).</summary>
    Task<IReadOnlyList<Guid>> ListContentIdsByPathAsync(IReadOnlyCollection<string> paths, CancellationToken cancellationToken);

    Task<IReadOnlyList<IndexedDocumentState>> ListStatesAsync(CancellationToken cancellationToken);
}
