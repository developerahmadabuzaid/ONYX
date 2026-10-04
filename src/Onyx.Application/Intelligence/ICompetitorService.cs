using Onyx.Contracts.Intelligence;
using Onyx.Domain.Common;

namespace Onyx.Application.Intelligence;

public interface ICompetitorService
{
    Task<IReadOnlyList<CompetitorResponse>> ListAsync(CancellationToken cancellationToken);

    Task<Result<CompetitorResponse>> CreateAsync(SaveCompetitorRequest request, CancellationToken cancellationToken);

    Task<Result<CompetitorResponse>> UpdateAsync(Guid id, SaveCompetitorRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
