using Onyx.Domain.SiteContent;

namespace Onyx.Application.SiteContent;

public interface ISiteContentRepository
{
    /// <summary>The document with this key (tracked), or null when it has never been saved.</summary>
    Task<SiteContentDocument?> GetAsync(string key, CancellationToken cancellationToken);

    Task<IReadOnlyList<SiteContentDocument>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Key → published JSON of every published document (untracked).</summary>
    Task<IReadOnlyDictionary<string, string>> ListPublishedAsync(CancellationToken cancellationToken);

    /// <summary>Published revisions of a document, newest first.</summary>
    Task<IReadOnlyList<SiteContentRevision>> ListRevisionsAsync(Guid documentId, int take, CancellationToken cancellationToken);

    Task<SiteContentRevision?> GetRevisionAsync(Guid documentId, int number, CancellationToken cancellationToken);

    /// <summary>True when a published document (or, with <paramref name="includeDrafts"/>, a working copy) references the media asset.</summary>
    Task<bool> IsMediaReferencedAsync(Guid mediaAssetId, bool includeDrafts, CancellationToken cancellationToken);

    void Add(SiteContentDocument document);

    void AddRevision(SiteContentRevision revision);

    void SetExpectedVersion(SiteContentDocument document, uint version);
}
