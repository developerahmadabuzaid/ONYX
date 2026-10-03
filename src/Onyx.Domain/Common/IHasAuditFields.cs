namespace Onyx.Domain.Common;

/// <summary>Creation/modification stamps. Set by the persistence layer, never by business code.</summary>
public interface IHasAuditFields
{
    DateTimeOffset CreatedAtUtc { get; }

    Guid? CreatedById { get; }

    DateTimeOffset? UpdatedAtUtc { get; }

    Guid? UpdatedById { get; }
}
