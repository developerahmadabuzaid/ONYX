using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Onyx.Application.Content;

/// <summary>
/// A hash of what one language of a page shows publicly (its text, FAQs, SEO settings, slug, featured image, call to action and
/// place in the hierarchy). Ordering and internal keys are excluded, so re-ordering or re-publishing identical content does not
/// count as a change for <c>lastmod</c>.
/// </summary>
public static class PublicPageFingerprint
{
    public static string For(ContentSnapshot snapshot, string languageCode)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var visible = new
        {
            snapshot.Type,
            snapshot.ParentId,
            snapshot.FeaturedImageId,
            snapshot.PrimaryCtaId,
            Translation = snapshot.Translation(languageCode),
        };

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(visible, ContentSnapshotSerializer.Options))));
    }
}
