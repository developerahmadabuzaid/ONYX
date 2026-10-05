using Onyx.Contracts.Common;
using Onyx.Contracts.Privacy;
using Onyx.Domain.Common;

namespace Onyx.Application.Privacy;

/// <summary>Admin-operated data-subject requests (export and erasure). Every operation requires <c>Privacy.Manage</c> at the API.</summary>
public interface IDataSubjectRequestService
{
    Task<Result<PagedResult<DataSubjectRequestResponse>>> ListAsync(DataSubjectRequestListQuery query, CancellationToken cancellationToken);

    Task<Result<DataSubjectRequestResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<DataSubjectRequestResponse>> CreateAsync(CreateDataSubjectRequestRequest request, CancellationToken cancellationToken);

    Task<Result<DataSubjectRequestResponse>> VerifyAsync(Guid id, VerifyDataSubjectRequestRequest request, CancellationToken cancellationToken);

    Task<Result<DataSubjectRequestResponse>> RejectAsync(Guid id, RejectDataSubjectRequestRequest request, CancellationToken cancellationToken);

    Task<Result<DataSubjectExportFile>> ExportAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<DataSubjectRequestResponse>> EraseAsync(Guid id, ConfirmErasureRequest request, CancellationToken cancellationToken);
}
