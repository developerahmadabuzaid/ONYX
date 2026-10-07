namespace Onyx.Contracts.Search;

/// <summary>One group (Services, Industries, Work, Insights, Glossary, Integrations, Pages); <see cref="Total"/> may exceed the items shown.</summary>
public sealed record SearchResultGroupResponse(string Group, int Total, IReadOnlyList<SearchResultItemResponse> Items);
