using Onyx.Contracts.Content;

namespace Onyx.Application.Content;

/// <summary>Anonymous, read-only access to live content. Drafts, archived content and previews are never returned here.</summary>
public interface IPublicContentService
{
    Task<ContentResolveResponse> ResolveAsync(string languageCode, string path, CancellationToken cancellationToken);

    Task<IReadOnlyList<SitemapEntryResponse>> GetSitemapAsync(string languageCode, CancellationToken cancellationToken);

    /// <summary>
    /// Live items of a public section (<c>work</c>, <c>case-studies</c>, <c>insights</c>, <c>topics</c>, <c>glossary</c>,
    /// <c>integrations</c>); insights can be narrowed to one topic. Newest insights first; other sections in editorial order.
    /// </summary>
    Task<Domain.Common.Result<Contracts.Common.PagedResult<ListingItemResponse>>> ListAsync(
        string languageCode, string section, Guid? topicId, int page, CancellationToken cancellationToken);

    /// <summary>The homepage's featured projects (at most three), in slot order, live only.</summary>
    Task<IReadOnlyList<ListingItemResponse>> GetFeaturedProjectsAsync(string languageCode, CancellationToken cancellationToken);
}
