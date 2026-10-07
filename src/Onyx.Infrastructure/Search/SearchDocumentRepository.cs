using Microsoft.EntityFrameworkCore;
using Onyx.Application.Search;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Search;

/// <summary>Writes and reads the search index. All statements use bound parameters.</summary>
internal sealed class SearchDocumentRepository(ApplicationDbContext dbContext, TimeProvider timeProvider) : ISearchDocumentRepository
{
    public async Task UpsertAsync(SearchDocumentData document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var title = Cap(document.Title, SearchDocument.TitleMaxLength);
        var summary = Cap(document.Summary, SearchDocument.SummaryMaxLength);
        var path = Cap(document.Path, SearchDocument.PathMaxLength);

        // One statement: concurrent re-indexing of the same page (event + reconcile) cannot create duplicates.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO search_documents (id, content_item_id, language_code, content_type, group_name, title, summary, path,
                published_at_utc, live_revision_id, indexed_at_utc, title_normalized, summary_normalized, body_normalized, names_normalized)
            VALUES ({Guid.CreateVersion7()}, {document.ContentItemId}, {document.LanguageCode}, {document.ContentType}, {document.Group}, {title}, {summary},
                {path}, {document.PublishedAtUtc}, {document.LiveRevisionId}, {timeProvider.GetUtcNow()}, {document.TitleNormalized},
                {document.SummaryNormalized}, {document.BodyNormalized}, {document.NamesNormalized})
            ON CONFLICT (content_item_id, language_code) DO UPDATE SET
                content_type = EXCLUDED.content_type, group_name = EXCLUDED.group_name, title = EXCLUDED.title, summary = EXCLUDED.summary,
                path = EXCLUDED.path, published_at_utc = EXCLUDED.published_at_utc, live_revision_id = EXCLUDED.live_revision_id,
                indexed_at_utc = EXCLUDED.indexed_at_utc, title_normalized = EXCLUDED.title_normalized,
                summary_normalized = EXCLUDED.summary_normalized, body_normalized = EXCLUDED.body_normalized,
                names_normalized = EXCLUDED.names_normalized
            """,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<int> RemoveAsync(Guid contentItemId, string? languageCode, CancellationToken cancellationToken) =>
        dbContext.SearchDocuments
            .Where(document => document.ContentItemId == contentItemId && (languageCode == null || document.LanguageCode == languageCode))
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListContentIdsByPathAsync(IReadOnlyCollection<string> paths, CancellationToken cancellationToken)
    {
        if (paths.Count == 0)
        {
            return [];
        }

        return await dbContext.SearchDocuments.AsNoTracking()
            .Where(document => paths.Contains(document.Path))
            .Select(document => document.ContentItemId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<IndexedDocumentState>> ListStatesAsync(CancellationToken cancellationToken) =>
        await dbContext.SearchDocuments.AsNoTracking()
            .Select(document => new IndexedDocumentState(document.ContentItemId, document.LanguageCode, document.LiveRevisionId, document.Path))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static string Cap(string value, int maxLength) => value.Length > maxLength ? value[..maxLength] : value;
}
