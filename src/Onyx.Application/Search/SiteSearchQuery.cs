namespace Onyx.Application.Search;

/// <param name="Text">Normalized words to find (<see cref="ParsedSearchQuery.Words"/>), for trigram and name matching.</param>
/// <param name="WebQuery">The query with phrase quotes and exclusions kept (<see cref="ParsedSearchQuery.WebQuery"/>).</param>
/// <param name="ExcludedWords">Normalized words joined with "or"; a result containing any of them is left out. Null when nothing is excluded.</param>
/// <param name="PrefixTerm">The last word (letters and digits only) for prefix matching, or null.</param>
/// <param name="PerGroup">Most results returned per group.</param>
public sealed record SiteSearchQuery(string LanguageCode, string Text, string WebQuery, string? ExcludedWords, string? PrefixTerm, int PerGroup);
