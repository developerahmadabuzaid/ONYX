namespace Onyx.Contracts.Privacy;

public sealed record SaveRetentionPolicyRequest(string Action, int? PeriodDays, bool IsEnabled, uint? Version);
