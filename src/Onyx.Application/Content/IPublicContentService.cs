using Onyx.Contracts.Content;

namespace Onyx.Application.Content;

/// <summary>Anonymous, read-only access to live content. Drafts, archived content and previews are never returned here.</summary>
public interface IPublicContentService
{
    Task<ContentResolveResponse> ResolveAsync(string languageCode, string path, CancellationToken cancellationToken);

    Task<IReadOnlyList<SitemapEntryResponse>> GetSitemapAsync(string languageCode, CancellationToken cancellationToken);
}
