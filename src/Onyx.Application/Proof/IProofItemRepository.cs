using Onyx.Domain.Proof;

namespace Onyx.Application.Proof;

public interface IProofItemRepository
{
    Task<ProofItem?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProofItem>> ListForContentAsync(Guid contentItemId, CancellationToken cancellationToken);

    /// <summary>Published items only (they are verified by construction), ordered for display.</summary>
    Task<IReadOnlyList<ProofItem>> ListPublishedForContentAsync(Guid contentItemId, CancellationToken cancellationToken);

    void Add(ProofItem item);

    void Remove(ProofItem item);

    void SetExpectedVersion(ProofItem item, uint version);
}
