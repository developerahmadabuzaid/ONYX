using Onyx.Contracts.SiteContent;

namespace Onyx.Web.ApiClients;

/// <summary>Admin editing of site content documents. The settings document goes through its own Settings.Manage endpoints.</summary>
public interface IAdminSiteContentApiClient
{
    Task<ApiResult<IReadOnlyList<SiteContentDocumentSummaryResponse>>> ListAsync(CancellationToken cancellationToken);

    Task<ApiResult<SiteContentDocumentResponse>> GetAsync(string key, CancellationToken cancellationToken);

    Task<ApiResult<SiteContentDocumentResponse>> SaveDraftAsync(string key, SaveSiteContentDraftRequest request, CancellationToken cancellationToken);

    Task<ApiResult<SiteContentDocumentResponse>> PublishAsync(string key, uint version, CancellationToken cancellationToken);

    Task<ApiResult<SiteContentDocumentResponse>> DiscardAsync(string key, uint version, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<SiteContentRevisionResponse>>> ListRevisionsAsync(string key, CancellationToken cancellationToken);

    Task<ApiResult<SiteContentDocumentResponse>> RestoreAsync(string key, int number, uint version, CancellationToken cancellationToken);
}
