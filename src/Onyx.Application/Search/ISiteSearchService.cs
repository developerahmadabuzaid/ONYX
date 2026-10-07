namespace Onyx.Application.Search;

/// <summary>
/// The search engine port (architecture.md §18, D25). v1 is PostgreSQL full-text + trigram; a semantic or external engine can
/// replace it later. It receives already normalized, length-capped text and must treat it strictly as data.
/// </summary>
public interface ISiteSearchService
{
    Task<IReadOnlyList<SearchHit>> SearchAsync(SiteSearchQuery query, CancellationToken cancellationToken);
}
