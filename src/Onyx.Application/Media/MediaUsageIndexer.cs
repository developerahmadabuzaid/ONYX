using Onyx.Application.Content;
using Onyx.Domain.Content;
using Onyx.Domain.Media;

namespace Onyx.Application.Media;

/// <summary>Rebuilds where a content item uses media: its working copy and, separately, its live revision.</summary>
public sealed class MediaUsageIndexer(IMediaRepository media)
{
    public Task RebuildAsync(Guid contentItemId, ContentSnapshot workingCopy, ContentSnapshot? live, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workingCopy);
        var usages = Collect(contentItemId, workingCopy, isLive: false).Concat(live is null ? [] : Collect(contentItemId, live, isLive: true))
            .DistinctBy(usage => (usage.MediaAssetId, usage.LanguageCode, usage.Context, usage.IsLive))
            .ToList();
        return media.ReplaceUsagesAsync(contentItemId, usages, cancellationToken);
    }

    public static IEnumerable<(Guid MediaAssetId, string LanguageCode, MediaUsageContext Context)> References(ContentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        foreach (var translation in snapshot.Translations)
        {
            if (snapshot.FeaturedImageId is { } featured)
            {
                yield return (featured, translation.LanguageCode, MediaUsageContext.FeaturedImage);
            }

            if (translation.Seo.OpenGraphImageId is { } openGraph)
            {
                yield return (openGraph, translation.LanguageCode, MediaUsageContext.OpenGraphImage);
            }

            foreach (var block in translation.Body)
            {
                if (block is { Type: ContentBlockType.Image, MediaAssetId: { } image })
                {
                    yield return (image, translation.LanguageCode, MediaUsageContext.Body);
                }
            }
        }
    }

    private static IEnumerable<MediaUsage> Collect(Guid contentItemId, ContentSnapshot snapshot, bool isLive) =>
        References(snapshot).Select(reference => MediaUsage.Create(reference.MediaAssetId, contentItemId, reference.LanguageCode, reference.Context, isLive));
}
