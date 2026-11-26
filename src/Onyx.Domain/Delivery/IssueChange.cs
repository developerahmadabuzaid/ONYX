namespace Onyx.Domain.Delivery;

/// <summary>One field changed on an issue, as plain display values (ids are resolved to names by the caller).</summary>
public sealed record IssueChange(string Field, string? OldValue, string? NewValue);
