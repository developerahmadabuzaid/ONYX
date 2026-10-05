namespace Onyx.Contracts.Leads;

/// <summary>Null <see cref="OwnerId"/> unassigns the lead.</summary>
public sealed record AssignLeadRequest(Guid? OwnerId, uint? Version);
