namespace Onyx.Contracts.Search;

/// <summary>Grouped results for one query and language. <see cref="Query"/> echoes the trimmed, length-capped input.</summary>
public sealed record SiteSearchResponse(string Query, string LanguageCode, int Total, IReadOnlyList<SearchResultGroupResponse> Groups);
