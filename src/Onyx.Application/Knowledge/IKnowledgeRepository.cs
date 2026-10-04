using Onyx.Domain.Knowledge;

namespace Onyx.Application.Knowledge;

public interface IKnowledgeRepository
{
    Task<KnowledgeEntry?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<KnowledgeEntry>> ListAsync(KnowledgeCategory? category, KnowledgeStatus? status, string? search, CancellationToken cancellationToken);

    void Add(KnowledgeEntry entry);

    void SetExpectedVersion(KnowledgeEntry entry, uint version);
}
