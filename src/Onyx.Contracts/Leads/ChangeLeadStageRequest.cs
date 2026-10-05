namespace Onyx.Contracts.Leads;

public sealed record ChangeLeadStageRequest(string Stage, uint? Version);
