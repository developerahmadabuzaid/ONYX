namespace Onyx.Application.Privacy;

/// <summary>An audit entry performed by the account (what and when; IP addresses and change details stay internal).</summary>
public sealed record AccountActivityExport(DateTimeOffset OccurredAtUtc, string Action, string Category, string EntityType);
