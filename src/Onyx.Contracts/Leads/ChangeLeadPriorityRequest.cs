namespace Onyx.Contracts.Leads;

public sealed record ChangeLeadPriorityRequest(string Priority, uint? Version);
