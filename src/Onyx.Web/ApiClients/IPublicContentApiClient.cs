using Onyx.Contracts.Content;

namespace Onyx.Web.ApiClients;

/// <summary>Anonymous public content calls (live pages, sitemap, previews, media).</summary>
public interface IPublicContentApiClient
{
    Task<ApiResult<ContentResolveResponse>> ResolveAsync(string languageCode, string path, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<SitemapEntryResponse>>> GetSitemapAsync(string languageCode, CancellationToken cancellationToken);

    Task<ApiResult<ContentPreviewResponse>> GetPreviewAsync(string token, CancellationToken cancellationToken);

    Task<ApiFileStream?> OpenMediaAsync(Guid id, string? previewToken, CancellationToken cancellationToken);
}
