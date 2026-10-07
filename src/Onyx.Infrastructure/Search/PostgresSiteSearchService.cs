using Microsoft.EntityFrameworkCore;
using Onyx.Application.Search;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Search;

/// <summary>
/// PostgreSQL site search (architecture.md §18): <c>websearch_to_tsquery</c> over the weighted, stemmed vector (Arabic or English
/// configuration by language), the unstemmed names vector (exact names and brands, plus a prefix match on the last word) and
/// trigram word similarity on titles (typos and partial words). Ranking: <c>ts_rank_cd</c> × group boost, plus a names bonus
/// and title similarity. Every value, including the query text, is a bound parameter; nothing user-supplied becomes SQL.
/// </summary>
internal sealed class PostgresSiteSearchService(ApplicationDbContext dbContext) : ISiteSearchService
{
    /// <summary>Minimum trigram word similarity for a title-only match (typo tolerance).</summary>
    public const double TitleSimilarityThreshold = 0.4;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SiteSearchQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var configuration = query.LanguageCode == "ar" ? "arabic" : "english";
        var prefix = query.PrefixTerm is null ? null : query.PrefixTerm + ":*";

        var rows = await dbContext.Database.SqlQuery<SearchRow>(
            $"""
            WITH q AS (
                SELECT websearch_to_tsquery(CAST({configuration} AS regconfig), {query.WebQuery}) AS fts,
                       websearch_to_tsquery('simple'::regconfig, {query.Text}) AS names,
                       CASE WHEN {prefix}::text IS NULL THEN NULL ELSE to_tsquery('simple'::regconfig, {prefix}::text) END AS prefix,
                       CASE WHEN {query.ExcludedWords}::text IS NULL THEN NULL
                            ELSE websearch_to_tsquery(CAST({configuration} AS regconfig), {query.ExcludedWords}::text) END AS excluded,
                       CASE WHEN {query.ExcludedWords}::text IS NULL THEN NULL
                            ELSE websearch_to_tsquery('simple'::regconfig, {query.ExcludedWords}::text) END AS excluded_names
            ),
            matches AS (
                SELECT d.group_name, d.content_type, d.title, d.summary, d.path,
                       ts_rank_cd(d.search_vector, q.fts, 32)::float8 * (CASE d.group_name
                           WHEN 'Services' THEN {SearchGroups.Boost(SearchGroups.Services)}
                           WHEN 'Industries' THEN {SearchGroups.Boost(SearchGroups.Industries)}
                           WHEN 'Work' THEN {SearchGroups.Boost(SearchGroups.Work)}
                           WHEN 'Insights' THEN {SearchGroups.Boost(SearchGroups.Insights)}
                           WHEN 'Glossary' THEN {SearchGroups.Boost(SearchGroups.Glossary)}
                           WHEN 'Integrations' THEN {SearchGroups.Boost(SearchGroups.Integrations)}
                           ELSE {SearchGroups.Boost(SearchGroups.Pages)} END)::float8
                       + (CASE WHEN d.names_vector @@ q.names THEN 0.5 ELSE 0 END)::float8
                       + 0.3 * word_similarity({query.Text}, d.title_normalized)::float8 AS score
                FROM search_documents d, q
                WHERE d.language_code = {query.LanguageCode}
                  AND (d.search_vector @@ q.fts
                       OR d.names_vector @@ q.names
                       OR (q.prefix IS NOT NULL AND d.names_vector @@ q.prefix)
                       OR word_similarity({query.Text}, d.title_normalized) >= {TitleSimilarityThreshold})
                  -- An excluded word rules a page out on every matching path, not only the full-text one.
                  AND NOT (q.excluded IS NOT NULL AND (d.search_vector @@ q.excluded OR d.names_vector @@ q.excluded_names))
            ),
            ranked AS (
                SELECT m.*, row_number() OVER (PARTITION BY m.group_name ORDER BY m.score DESC, m.title) AS position,
                       count(*) OVER (PARTITION BY m.group_name) AS group_total
                FROM matches m
            )
            SELECT group_name AS "GroupName", content_type AS "ContentType", title AS "Title", summary AS "Summary", path AS "Path",
                   score AS "Score", group_total AS "GroupTotal"
            FROM ranked
            WHERE position <= {query.PerGroup}
            """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(row => new SearchHit(row.GroupName, row.ContentType, row.Title, row.Summary, row.Path, row.Score, (int)row.GroupTotal)).ToList();
    }

    private sealed class SearchRow
    {
        public string GroupName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public double Score { get; set; }

        public long GroupTotal { get; set; }
    }
}
