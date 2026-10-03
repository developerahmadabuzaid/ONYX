namespace Onyx.Domain.Common;

public abstract class AuditableEntity : Entity, IHasAuditFields
{
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid? CreatedById { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public Guid? UpdatedById { get; private set; }
}
