using System.Globalization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Privacy;

namespace Onyx.Web.ApiClients;

public sealed class AdminPrivacyApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IAdminPrivacyApiClient
{
    private const string Requests = "api/v1/admin/data-subject-requests";
    private const string Policies = "api/v1/admin/retention-policies";

    public Task<ApiResult<PagedResult<DataSubjectRequestResponse>>> ListRequestsAsync(string? status, int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<DataSubjectRequestResponse>>(
            $"{Requests}?page={page.ToString(CultureInfo.InvariantCulture)}&pageSize=25{(string.IsNullOrWhiteSpace(status) ? string.Empty : "&status=" + Uri.EscapeDataString(status))}",
            cancellationToken);

    public Task<ApiResult<DataSubjectRequestResponse>> GetRequestAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<DataSubjectRequestResponse>($"{Requests}/{id:D}", cancellationToken);

    public Task<ApiResult<DataSubjectRequestResponse>> CreateRequestAsync(CreateDataSubjectRequestRequest request, CancellationToken cancellationToken) =>
        PostAsync<DataSubjectRequestResponse>(Requests, request, cancellationToken);

    public Task<ApiResult<DataSubjectRequestResponse>> VerifyRequestAsync(Guid id, VerifyDataSubjectRequestRequest request, CancellationToken cancellationToken) =>
        PostAsync<DataSubjectRequestResponse>($"{Requests}/{id:D}/verify", request, cancellationToken);

    public Task<ApiResult<DataSubjectRequestResponse>> RejectRequestAsync(Guid id, RejectDataSubjectRequestRequest request, CancellationToken cancellationToken) =>
        PostAsync<DataSubjectRequestResponse>($"{Requests}/{id:D}/reject", request, cancellationToken);

    public Task<ApiResult<DataSubjectExportFile>> ExportAsync(Guid id, CancellationToken cancellationToken) =>
        PostAsync<DataSubjectExportFile>($"{Requests}/{id:D}/export", null, cancellationToken);

    public Task<ApiResult<DataSubjectRequestResponse>> EraseAsync(Guid id, ConfirmErasureRequest request, CancellationToken cancellationToken) =>
        PostAsync<DataSubjectRequestResponse>($"{Requests}/{id:D}/erase", request, cancellationToken);

    public Task<ApiResult<IReadOnlyList<RetentionPolicyResponse>>> ListPoliciesAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<RetentionPolicyResponse>>(Policies, cancellationToken);

    public Task<ApiResult<RetentionPolicyResponse>> SavePolicyAsync(string category, SaveRetentionPolicyRequest request, CancellationToken cancellationToken) =>
        PutAsync<RetentionPolicyResponse>($"{Policies}/{Uri.EscapeDataString(category)}", request, cancellationToken);

    public Task<ApiResult<RetentionPreviewResponse>> PreviewPolicyAsync(string category, string action, int periodDays, CancellationToken cancellationToken) =>
        GetAsync<RetentionPreviewResponse>(
            $"{Policies}/{Uri.EscapeDataString(category)}/preview?action={Uri.EscapeDataString(action)}&periodDays={periodDays.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken);
}
