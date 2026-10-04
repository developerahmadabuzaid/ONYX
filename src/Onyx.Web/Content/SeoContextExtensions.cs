using Onyx.Contracts.Content;
using Onyx.Web.Localization;

namespace Onyx.Web.Content;

public static class SeoContextExtensions
{
    /// <summary>Copies a live page's SEO data into the request's <see cref="SeoContext"/>, percent-encoding every path.</summary>
    public static void Apply(this SeoContext seo, PublicContentResponse content, string baseUrl, string organizationName)
    {
        ArgumentNullException.ThrowIfNull(seo);
        ArgumentNullException.ThrowIfNull(content);
        var data = content.Seo;
        seo.Title = data.Title;
        seo.Description = data.Description;
        seo.CanonicalPath = SitePaths.Encode(data.CanonicalPath);
        foreach (var (language, path) in data.Alternates)
        {
            seo.Alternates[language] = SitePaths.Encode(path);
        }

        seo.XDefaultPath = data.XDefaultPath is null ? null : SitePaths.Encode(data.XDefaultPath);
        seo.NoIndex = !data.Index;
        seo.NoFollow = !data.Follow;
        seo.OpenGraphTitle = data.OpenGraphTitle ?? data.Title;
        seo.OpenGraphDescription = data.OpenGraphDescription ?? data.Description;
        var image = data.OpenGraphImage ?? content.FeaturedImage;
        seo.OpenGraphImagePath = image is null ? null : $"/media/{image.Id:D}";
        seo.OpenGraphImageAlt = image?.Alt;
        seo.StructuredDataJson = StructuredDataBuilder.Build(content, baseUrl, organizationName);
    }
}
