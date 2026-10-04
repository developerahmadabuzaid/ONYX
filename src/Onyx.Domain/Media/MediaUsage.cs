using Onyx.Domain.Common;

namespace Onyx.Domain.Media;

/// <summary>
/// Where a media asset is used: a content item's working copy (<see cref="IsLive"/> false) or its live revision (true).
/// Rebuilt whenever content is saved or its publication changes; used to block deleting assets that pages depend on.
/// </summary>
public sealed class MediaUsage : Entity
{
    private MediaUsage()
    {
        LanguageCode = string.Empty;
    }

    public Guid MediaAssetId { get; private set; }

    public Guid ContentItemId { get; private set; }

    public string LanguageCode { get; private set; }

    public MediaUsageContext Context { get; private set; }

    public bool IsLive { get; private set; }

    public static MediaUsage Create(Guid mediaAssetId, Guid contentItemId, string languageCode, MediaUsageContext context, bool isLive) =>
        new() { MediaAssetId = mediaAssetId, ContentItemId = contentItemId, LanguageCode = languageCode, Context = context, IsLive = isLive };
}
