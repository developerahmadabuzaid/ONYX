namespace Onyx.Application.Search;

/// <param name="Words">The normalized words a result should contain (excluded words removed).</param>
/// <param name="WebQuery">All words in bare form, with phrase quotes and exclusions kept, for <c>websearch_to_tsquery</c>.</param>
/// <param name="ExcludedWords">The words a result must not contain, original and bare forms; empty when nothing is excluded.</param>
public sealed record ParsedSearchQuery(string Words, string WebQuery, string ExcludedWords);
