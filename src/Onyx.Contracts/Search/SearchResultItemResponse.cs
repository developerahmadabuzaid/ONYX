namespace Onyx.Contracts.Search;

/// <summary><see cref="Path"/> is the page's current site path (unencoded, with the language prefix).</summary>
public sealed record SearchResultItemResponse(string Title, string Summary, string Path, string ContentType);
