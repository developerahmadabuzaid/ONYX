using Onyx.Domain.Auditing;

namespace Onyx.Application.Auditing;

/// <summary>
/// Explicit audit entries for security and administrative events (entity changes are audited automatically).
/// Entries join the current unit of work. <paramref name="details"/> must never contain secrets, tokens or passwords.
/// </summary>
public interface IAuditTrail
{
    void Record(
        AuditAction action,
        AuditCategory category,
        string entityType,
        string? entityId,
        object? details = null,
        Guid? actorUserId = null);
}
