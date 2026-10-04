using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Onyx.Contracts.Content;
using Onyx.Web.ApiClients;
using Onyx.Web.Configuration;
using Onyx.Web.Localization;

namespace Onyx.Web.Controllers;

/// <summary>
/// <c>/sitemap.xml</c> (index) and <c>/sitemaps/{lang}.xml</c>: live, indexable, self-canonical pages only, with hreflang alternates.
/// <c>/robots.txt</c> blocks everything until launch (<see cref="SiteOptions.AllowIndexing"/>).
/// </summary>
public sealed class SitemapController(IPublicContentApiClient api, LanguageCatalog languages, IOptions<SiteOptions> site) : Controller
{
    private const string SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private const string XhtmlNamespace = "http://www.w3.org/1999/xhtml";

    [HttpGet("/robots.txt")]
    public IActionResult Robots()
    {
        var builder = new StringBuilder("User-agent: *\n");
        if (!site.Value.AllowIndexing)
        {
            builder.Append("Disallow: /\n");
        }
        else
        {
            builder.Append("Disallow: /admin\n");
            foreach (var language in languages.Languages)
            {
                builder.Append("Disallow: /").Append(language.Code).Append("/preview\n");
                builder.Append("Disallow: /").Append(language.Code).Append("/language\n");
            }

            builder.Append("Disallow: /media/*?preview=\n");
            builder.Append("\nSitemap: ").Append(SitePaths.Absolute(site.Value.PublicBaseUrl, "/sitemap.xml")).Append('\n');
        }

        return Content(builder.ToString(), "text/plain; charset=utf-8");
    }

    [HttpGet("/sitemap.xml")]
    public IActionResult Index()
    {
        MarkNoIndexBeforeLaunch();
        var document = Write(writer =>
        {
            writer.WriteStartElement("sitemapindex", SitemapNamespace);
            foreach (var language in languages.Languages)
            {
                writer.WriteStartElement("sitemap", SitemapNamespace);
                writer.WriteElementString("loc", SitemapNamespace, SitePaths.Absolute(site.Value.PublicBaseUrl, $"/sitemaps/{language.Code}.xml"));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        });
        return Content(document, "application/xml; charset=utf-8");
    }

    [HttpGet("/sitemaps/{lang:onyxlang}.xml")]
    public async Task<IActionResult> Language(string lang, CancellationToken cancellationToken)
    {
        var result = await api.GetSitemapAsync(lang, cancellationToken);
        if (result.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        if (!result.IsSuccess || result.Value is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        MarkNoIndexBeforeLaunch();
        var home = new SitemapEntryResponse($"/{lang}/", null, languages.Languages.ToDictionary(language => language.Code, language => $"/{language.Code}/", StringComparer.Ordinal));
        var entries = new[] { home }.Concat(result.Value);
        var document = Write(writer =>
        {
            writer.WriteStartElement("urlset", SitemapNamespace);
            writer.WriteAttributeString("xmlns", "xhtml", null, XhtmlNamespace);
            foreach (var entry in entries)
            {
                writer.WriteStartElement("url", SitemapNamespace);
                writer.WriteElementString("loc", SitemapNamespace, AbsoluteUrl(entry.Path));
                if (entry.LastModifiedUtc is { } modified)
                {
                    writer.WriteElementString("lastmod", SitemapNamespace, modified.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture));
                }

                if (entry.Alternates.Count > 1)
                {
                    foreach (var (language, path) in entry.Alternates.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    {
                        WriteAlternate(writer, language, path);
                    }

                    if (entry.Alternates.TryGetValue(languages.DefaultLanguage, out var defaultPath))
                    {
                        WriteAlternate(writer, "x-default", defaultPath);
                    }
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        });
        return Content(document, "application/xml; charset=utf-8");
    }

    private void WriteAlternate(XmlWriter writer, string hreflang, string path)
    {
        writer.WriteStartElement("xhtml", "link", XhtmlNamespace);
        writer.WriteAttributeString("rel", "alternate");
        writer.WriteAttributeString("hreflang", hreflang);
        writer.WriteAttributeString("href", AbsoluteUrl(path));
        writer.WriteEndElement();
    }

    private string AbsoluteUrl(string path) => SitePaths.Absolute(site.Value.PublicBaseUrl, SitePaths.Encode(path));

    private void MarkNoIndexBeforeLaunch()
    {
        if (!site.Value.AllowIndexing)
        {
            Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        }
    }

    private static string Write(Action<XmlWriter> body)
    {
        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings { Indent = false, OmitXmlDeclaration = true }))
        {
            body(writer);
        }

        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + builder;
    }
}
