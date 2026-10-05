namespace Onyx.Contracts.Leads;

public sealed record LeadActivityResponse(Guid Id, string Kind, string? Detail, Guid? RelatedId, Guid? ActorId, DateTimeOffset OccurredAtUtc);
