using Onyx.Domain.Common;

namespace Onyx.Domain.SiteContent;

/// <summary>An immutable copy of a published <see cref="SiteContentDocument"/> version, kept for review and restore.</summary>
public sealed class SiteContentRevision : Entity
{
    private SiteContentRevision()
    {
        Json = string.Empty;
        MediaIds = [];
    }

    public Guid DocumentId { get; private set; }

    public int Number { get; private set; }

    public string Json { get; private set; }

    public List<Guid> MediaIds { get; private set; }

    public DateTimeOffset PublishedAtUtc { get; private set; }

    public Guid? PublishedById { get; private set; }

    internal static SiteContentRevision Create(Guid documentId, int number, string json, IEnumerable<Guid> mediaIds, DateTimeOffset publishedAtUtc, Guid? publishedById) =>
        new()
        {
            DocumentId = documentId,
            Number = number,
            Json = json,
            MediaIds = mediaIds.ToList(),
            PublishedAtUtc = publishedAtUtc,
            PublishedById = publishedById,
        };
}
