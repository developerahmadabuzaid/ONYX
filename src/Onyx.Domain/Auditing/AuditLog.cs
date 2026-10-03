using Onyx.Domain.Common;

namespace Onyx.Domain.Auditing;

/// <summary>Append-only record of a sensitive, editorial or operational change. Never updated or deleted through the application.</summary>
public sealed class AuditLog : Entity
{
    public const int EntityTypeMaxLength = 200;
    public const int EntityIdMaxLength = 100;
    public const int UserNameMaxLength = 256;
    public const int IpAddressMaxLength = 45;
    public const int CorrelationIdMaxLength = 64;

    private AuditLog()
    {
        EntityType = string.Empty;
    }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public AuditAction Action { get; private set; }

    public AuditCategory Category { get; private set; }

    public string EntityType { get; private set; }

    public string? EntityId { get; private set; }

    /// <summary>JSON change set (property → old/new). Sensitive values are already redacted by the time they reach here.</summary>
    public string? Changes { get; private set; }

    public Guid? UserId { get; private set; }

    public string? UserName { get; private set; }

    public string? IpAddress { get; private set; }

    public string? CorrelationId { get; private set; }

    public static AuditLog Record(
        DateTimeOffset occurredAtUtc,
        AuditAction action,
        AuditCategory category,
        string entityType,
        string? entityId,
        string? changes,
        Guid? userId,
        string? userName,
        string? ipAddress,
        string? correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        if (occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Audit timestamps must be UTC.", nameof(occurredAtUtc));
        }

        return new AuditLog
        {
            OccurredAtUtc = occurredAtUtc,
            Action = action,
            Category = category,
            EntityType = Truncate(entityType, EntityTypeMaxLength)!,
            EntityId = Truncate(entityId, EntityIdMaxLength),
            Changes = changes,
            UserId = userId,
            UserName = Truncate(userName, UserNameMaxLength),
            IpAddress = Truncate(ipAddress, IpAddressMaxLength),
            CorrelationId = Truncate(correlationId, CorrelationIdMaxLength),
        };
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;
}
