using Onyx.Contracts.Content;
using Onyx.Contracts.Organization;

namespace Onyx.Web.ApiClients;

/// <summary>Anonymous public content calls (live pages, sitemap, previews, media).</summary>
public interface IPublicContentApiClient
{
    Task<ApiResult<ContentResolveResponse>> ResolveAsync(string languageCode, string path, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<SitemapEntryResponse>>> GetSitemapAsync(string languageCode, CancellationToken cancellationToken);

    Task<ApiResult<ContentPreviewResponse>> GetPreviewAsync(string token, CancellationToken cancellationToken);

    Task<ApiFileStream?> OpenMediaAsync(Guid id, string? previewToken, CancellationToken cancellationToken);

    /// <summary>Owner-verified organization facts, or null while they are incomplete or switched off.</summary>
    Task<PublicOrganizationResponse?> GetOrganizationAsync(CancellationToken cancellationToken);

    /// <summary>The IndexNow key for the key file, or null while IndexNow is inactive.</summary>
    Task<string?> GetIndexNowKeyAsync(CancellationToken cancellationToken);
}
