using Onyx.Domain.Content;

namespace Onyx.Application.Content;

public interface IContentRevisionRepository
{
    void Add(ContentRevision revision);

    Task<ContentRevision?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, ContentRevision>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<ContentRevision?> GetByNumberAsync(Guid contentItemId, int number, CancellationToken cancellationToken);

    Task<ContentRevision?> GetLatestAsync(Guid contentItemId, CancellationToken cancellationToken);

    /// <summary>Newest first, metadata only.</summary>
    Task<IReadOnlyList<RevisionSummary>> ListAsync(Guid contentItemId, int take, CancellationToken cancellationToken);
}
