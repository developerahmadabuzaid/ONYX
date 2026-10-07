using Onyx.Contracts.Search;

namespace Onyx.Web.Search;

/// <summary>The query as shown back, the grouped results (null before a search or on error) and whether the limit was hit.</summary>
public sealed record SearchPageModel(string Query, SiteSearchResponse? Results, bool RateLimited);
