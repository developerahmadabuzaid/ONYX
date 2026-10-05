using Onyx.Contracts.Common;
using Onyx.Domain.Privacy;

namespace Onyx.Application.Privacy;

public interface IDataSubjectRequestRepository
{
    Task<DataSubjectRequest?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads the request with a row lock (<c>FOR UPDATE</c>) inside the current transaction, serializing its execution.</summary>
    Task<DataSubjectRequest?> LockAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DataSubjectRequest>> ListForSubjectAsync(string subjectEmailHash, CancellationToken cancellationToken);

    Task<PagedResult<DataSubjectRequest>> ListAsync(DataSubjectRequestStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    void Add(DataSubjectRequest request);

    void SetExpectedVersion(DataSubjectRequest request, uint version);
}
