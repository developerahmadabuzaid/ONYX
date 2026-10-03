namespace Onyx.Contracts.Administration;

public sealed record AuditLogQuery(
    Guid? UserId = null,
    string? Category = null,
    string? Action = null,
    string? EntityType = null,
    string? EntityId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Page = 1,
    int PageSize = 50);
