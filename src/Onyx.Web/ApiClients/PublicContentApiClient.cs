using Onyx.Contracts.Content;
using Onyx.Contracts.Organization;

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

    public async Task<PublicOrganizationResponse?> GetOrganizationAsync(CancellationToken cancellationToken)
    {
        var result = await GetAsync<PublicOrganizationResponse>("api/v1/organization", cancellationToken).ConfigureAwait(false);
        // Structured data is optional: an unreachable API simply means no Organization node, never a failed page.
        return result.IsSuccess ? result.Value : null;
    }

    public async Task<string?> GetIndexNowKeyAsync(CancellationToken cancellationToken)
    {
        await using var file = await GetStreamAsync("api/v1/search-engine-verification/indexnow-key", cancellationToken).ConfigureAwait(false);
        if (file is null)
        {
            return null;
        }

        using var reader = new StreamReader(file.Content);
        var key = (await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false)).Trim();
        return key.Length is > 0 and <= 128 ? key : null;
    }
}
