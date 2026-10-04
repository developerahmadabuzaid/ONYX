using Onyx.Domain.Intelligence;

namespace Onyx.Application.Intelligence;

public interface ICompetitorRepository
{
    Task<CompetitorReference?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CompetitorReference>> ListAsync(CancellationToken cancellationToken);

    Task<bool> DomainExistsAsync(string domain, Guid? excludeId, CancellationToken cancellationToken);

    void Add(CompetitorReference reference);

    void Remove(CompetitorReference reference);

    void SetExpectedVersion(CompetitorReference reference, uint version);
}
