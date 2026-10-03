namespace Onyx.Domain.Common;

/// <summary>Deleting an entity of this type marks it deleted instead of removing the row; it disappears from normal queries.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAtUtc { get; }

    Guid? DeletedById { get; }
}
