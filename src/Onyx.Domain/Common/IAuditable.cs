using Onyx.Domain.Auditing;

namespace Onyx.Domain.Common;

/// <summary>Changes to entities of this type are written to the audit log (with sensitive values redacted).</summary>
public interface IAuditable
{
    AuditCategory AuditCategory => AuditCategory.Content;
}
