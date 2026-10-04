using Onyx.Domain.Content;

namespace Onyx.Application.Content;

public interface IContentRelationRepository
{
    Task<IReadOnlyList<ContentRelation>> ListFromAsync(Guid fromContentItemId, CancellationToken cancellationToken);

    Task<ContentRelation?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid fromId, Guid toId, RelationKind kind, CancellationToken cancellationToken);

    void Add(ContentRelation relation);

    void Remove(ContentRelation relation);
}
