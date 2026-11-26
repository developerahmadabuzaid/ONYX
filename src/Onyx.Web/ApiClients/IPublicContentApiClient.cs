using Onyx.Contracts.Common;
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

    /// <summary>Live items of a public section; null when the section does not exist.</summary>
    Task<PagedResult<ListingItemResponse>?> ListAsync(string languageCode, string section, Guid? topicId, int page, CancellationToken cancellationToken);

    /// <summary>The homepage's featured projects; empty when there are none or the API is unreachable.</summary>
    Task<IReadOnlyList<ListingItemResponse>> GetFeaturedProjectsAsync(string languageCode, CancellationToken cancellationToken);

    /// <summary>The homepage's newest published insights (at most <paramref name="count"/>); empty when there are none or the API is unreachable.</summary>
    Task<IReadOnlyList<ListingItemResponse>> GetLatestInsightsAsync(string languageCode, int count, CancellationToken cancellationToken);
}
