using Onyx.Domain.Knowledge;

namespace Onyx.Application.Knowledge;

public interface IInformationRequestRepository
{
    Task<InformationRequest?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<InformationRequest>> ListAsync(InformationRequestStatus? status, CancellationToken cancellationToken);

    void Add(InformationRequest request);
}
