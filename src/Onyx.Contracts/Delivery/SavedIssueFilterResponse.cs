namespace Onyx.Contracts.Delivery;

/// <summary>A saved issue search. <see cref="Query"/> is a query string of filter keys and values.</summary>
public sealed record SavedIssueFilterResponse(Guid Id, string Name, string Query, DateTimeOffset CreatedAtUtc);
