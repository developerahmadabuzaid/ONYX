namespace Onyx.Contracts.News;

/// <summary>The hub (no <see cref="Category"/>) or one category page; <see cref="Alternates"/> maps each language to its path.</summary>
public sealed record PublicNewsListResponse(
    string LanguageCode,
    PublicNewsCategoryResponse? Category,
    IReadOnlyList<PublicNewsItemSummaryResponse> Items,
    int Page,
    int PageSize,
    long TotalCount,
    IReadOnlyList<PublicNewsCategoryResponse> Categories,
    IReadOnlyDictionary<string, string> Alternates);
