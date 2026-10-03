namespace Onyx.Domain.Auditing;

/// <summary>Stored as text. Append new values only; never renumber or rename existing ones.</summary>
public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
    SoftDeleted = 3,
}
