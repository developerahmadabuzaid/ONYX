using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Onyx.Application.Content;

/// <summary>
/// A hash of what one language of a page shows to visitors and search engines: its visible content (title, summary, body, FAQs),
/// its URL (slug and place in the hierarchy), how it is presented (type, presentation, featured image, call to action) and the
/// SEO metadata that is actually served (meta title and description, canonical, robots, structured-data selection, Open Graph).
/// Internal planning fields (primary and secondary keywords, search intent, target market, SEO status and notes), ordering and
/// keys are excluded, so they never move <c>lastmod</c>. Each language is fingerprinted independently.
/// </summary>
public static class PublicPageFingerprint
{
    public static string For(ContentSnapshot snapshot, string languageCode)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var translation = snapshot.Translation(languageCode);
        var visible = new
        {
            snapshot.Type,
            snapshot.Presentation,
            snapshot.ParentId,
            snapshot.FeaturedImageId,
            snapshot.PrimaryCtaId,
            Page = translation is null
                ? null
                : new
                {
                    translation.Title,
                    translation.Summary,
                    translation.Slug,
                    translation.Body,
                    translation.Faqs,
                    Seo = new
                    {
                        translation.Seo.MetaTitle,
                        translation.Seo.MetaDescription,
                        translation.Seo.CanonicalPath,
                        translation.Seo.Index,
                        translation.Seo.Follow,
                        translation.Seo.StructuredData,
                        translation.Seo.OpenGraphTitle,
                        translation.Seo.OpenGraphDescription,
                        translation.Seo.OpenGraphImageId,
                    },
                },
        };

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(visible, ContentSnapshotSerializer.Options))));
    }
}
