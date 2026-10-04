using Onyx.Contracts.Content;

namespace Onyx.Web.ApiClients;

public sealed class PublicContentApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IPublicContentApiClient
{
    public Task<ApiResult<ContentResolveResponse>> ResolveAsync(string languageCode, string path, CancellationToken cancellationToken) =>
        GetAsync<ContentResolveResponse>($"api/v1/content/resolve?language={Uri.EscapeDataString(languageCode)}&path={Uri.EscapeDataString(path)}", cancellationToken);

    public Task<ApiResult<IReadOnlyList<SitemapEntryResponse>>> GetSitemapAsync(string languageCode, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<SitemapEntryResponse>>($"api/v1/content/sitemap?language={Uri.EscapeDataString(languageCode)}", cancellationToken);

    public Task<ApiResult<ContentPreviewResponse>> GetPreviewAsync(string token, CancellationToken cancellationToken) =>
        GetAsync<ContentPreviewResponse>($"api/v1/content/preview?token={Uri.EscapeDataString(token)}", cancellationToken);

    public Task<ApiFileStream?> OpenMediaAsync(Guid id, string? previewToken, CancellationToken cancellationToken) =>
        GetStreamAsync(previewToken is null ? $"api/v1/media/{id:D}" : $"api/v1/media/{id:D}?previewToken={Uri.EscapeDataString(previewToken)}", cancellationToken);
}
