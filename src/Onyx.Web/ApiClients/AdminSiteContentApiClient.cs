using System.Globalization;
using Onyx.Contracts.SiteContent;

namespace Onyx.Web.ApiClients;

public sealed class AdminSiteContentApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IAdminSiteContentApiClient
{
    public Task<ApiResult<IReadOnlyList<SiteContentDocumentSummaryResponse>>> ListAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<SiteContentDocumentSummaryResponse>>("api/v1/admin/site-content", cancellationToken);

    public Task<ApiResult<SiteContentDocumentResponse>> GetAsync(string key, CancellationToken cancellationToken) =>
        GetAsync<SiteContentDocumentResponse>(Base(key), cancellationToken);

    public Task<ApiResult<SiteContentDocumentResponse>> SaveDraftAsync(string key, SaveSiteContentDraftRequest request, CancellationToken cancellationToken) =>
        PutAsync<SiteContentDocumentResponse>($"{Base(key)}/draft", request, cancellationToken);

    public Task<ApiResult<SiteContentDocumentResponse>> PublishAsync(string key, uint version, CancellationToken cancellationToken) =>
        PostAsync<SiteContentDocumentResponse>($"{Base(key)}/publish", new SiteContentVersionRequest(version), cancellationToken);

    public Task<ApiResult<SiteContentDocumentResponse>> DiscardAsync(string key, uint version, CancellationToken cancellationToken) =>
        PostAsync<SiteContentDocumentResponse>($"{Base(key)}/discard", new SiteContentVersionRequest(version), cancellationToken);

    public Task<ApiResult<IReadOnlyList<SiteContentRevisionResponse>>> ListRevisionsAsync(string key, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<SiteContentRevisionResponse>>($"{Base(key)}/revisions", cancellationToken);

    public Task<ApiResult<SiteContentDocumentResponse>> RestoreAsync(string key, int number, uint version, CancellationToken cancellationToken) =>
        PostAsync<SiteContentDocumentResponse>($"{Base(key)}/revisions/{number.ToString(CultureInfo.InvariantCulture)}/restore", new SiteContentVersionRequest(version), cancellationToken);

    // The settings document has its own endpoints (Settings.Manage); every other key is page content (Pages.*).
    private static string Base(string key) =>
        key == SiteContentKeys.Settings ? "api/v1/admin/site-settings" : $"api/v1/admin/site-content/{Uri.EscapeDataString(key)}";
}
