using NpgsqlTypes;

namespace Onyx.Infrastructure.Search;

/// <summary>
/// One live public page in one language (architecture.md §18). A denormalized read model maintained by the indexer; holds only
/// published public text, never personal data. The two <c>tsvector</c> columns and the trigram index are computed by PostgreSQL.
/// </summary>
public sealed class SearchDocument
{
    public const int TitleMaxLength = 300;
    public const int SummaryMaxLength = 1_000;
    public const int PathMaxLength = 1_000;

    private SearchDocument()
    {
        LanguageCode = string.Empty;
        ContentType = string.Empty;
        GroupName = string.Empty;
        Title = string.Empty;
        Summary = string.Empty;
        Path = string.Empty;
        TitleNormalized = string.Empty;
        SummaryNormalized = string.Empty;
        BodyNormalized = string.Empty;
        NamesNormalized = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid ContentItemId { get; private set; }

    public string LanguageCode { get; private set; }

    public string ContentType { get; private set; }

    public string GroupName { get; private set; }

    public string Title { get; private set; }

    public string Summary { get; private set; }

    public string Path { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public Guid LiveRevisionId { get; private set; }

    public DateTimeOffset IndexedAtUtc { get; private set; }

    public string TitleNormalized { get; private set; }

    public string SummaryNormalized { get; private set; }

    public string BodyNormalized { get; private set; }

    public string NamesNormalized { get; private set; }

    /// <summary>Weighted (title A, summary B, body C), stemmed with the row language's configuration. Generated.</summary>
    public NpgsqlTsVector? SearchVector { get; private set; }

    /// <summary>Unstemmed (<c>simple</c>) title and slug words, so exact names and brands always match. Generated.</summary>
    public NpgsqlTsVector? NamesVector { get; private set; }
}
