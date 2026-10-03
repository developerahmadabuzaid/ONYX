namespace Onyx.Contracts.Administration;

public sealed record AuditLogResponse(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string Action,
    string Category,
    string EntityType,
    string? EntityId,
    string? Changes,
    Guid? UserId,
    string? IpAddress,
    string? CorrelationId);
