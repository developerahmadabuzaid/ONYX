using Onyx.Contracts.Common;
using Onyx.Contracts.Privacy;

namespace Onyx.Web.ApiClients;

/// <summary>Data-subject requests and retention policies. The API enforces <c>Privacy.Manage</c> on every call.</summary>
public interface IAdminPrivacyApiClient
{
    Task<ApiResult<PagedResult<DataSubjectRequestResponse>>> ListRequestsAsync(string? status, int page, CancellationToken cancellationToken);

    Task<ApiResult<DataSubjectRequestResponse>> GetRequestAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<DataSubjectRequestResponse>> CreateRequestAsync(CreateDataSubjectRequestRequest request, CancellationToken cancellationToken);

    Task<ApiResult<DataSubjectRequestResponse>> VerifyRequestAsync(Guid id, VerifyDataSubjectRequestRequest request, CancellationToken cancellationToken);

    Task<ApiResult<DataSubjectRequestResponse>> RejectRequestAsync(Guid id, RejectDataSubjectRequestRequest request, CancellationToken cancellationToken);

    Task<ApiResult<DataSubjectExportFile>> ExportAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<DataSubjectRequestResponse>> EraseAsync(Guid id, ConfirmErasureRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<RetentionPolicyResponse>>> ListPoliciesAsync(CancellationToken cancellationToken);

    Task<ApiResult<RetentionPolicyResponse>> SavePolicyAsync(string category, SaveRetentionPolicyRequest request, CancellationToken cancellationToken);

    Task<ApiResult<RetentionPreviewResponse>> PreviewPolicyAsync(string category, string action, int periodDays, CancellationToken cancellationToken);
}
