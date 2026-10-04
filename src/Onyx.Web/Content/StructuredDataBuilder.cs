using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using Onyx.Contracts.Content;
using Onyx.Contracts.Organization;
using Onyx.Web.Localization;

namespace Onyx.Web.Content;

/// <summary>
/// JSON-LD for a live page, limited to the types the API reports as supported by the visible content. Nothing is added that the
/// page does not show (no ratings, prices, addresses or areas served).
/// </summary>
public static class StructuredDataBuilder
{
    // Every encoder except UnsafeRelaxedJsonEscaping escapes <, >, & and quotes, so the output cannot close the script element.
    private static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    /// <summary>
    /// Home page JSON-LD: a WebSite node (site name; must be on the home page) and, only when the API returned owner-verified
    /// facts, an Organization node with exactly those facts. No address, phone or profile is added without a verified source.
    /// </summary>
    public static string BuildHome(string languageCode, string baseUrl, string siteName, PublicOrganizationResponse? organization)
    {
        var origin = (baseUrl ?? string.Empty).TrimEnd('/');
        var website = new JsonObject
        {
            ["@type"] = "WebSite",
            ["@id"] = origin + "/#website",
            ["url"] = origin + "/",
            ["name"] = siteName,
            ["inLanguage"] = languageCode,
        };
        var graph = new JsonArray { website };

        if (organization is not null)
        {
            var node = new JsonObject
            {
                ["@type"] = "Organization",
                ["@id"] = origin + "/#organization",
                ["name"] = organization.Name,
                ["url"] = organization.Url,
                ["logo"] = new JsonObject
                {
                    ["@type"] = "ImageObject",
                    ["url"] = origin + $"/media/{organization.LogoMediaId:D}",
                },
            };
            if (organization.LogoWidth is { } width && organization.LogoHeight is { } height)
            {
                node["logo"]!["width"] = width;
                node["logo"]!["height"] = height;
            }

            if (!string.IsNullOrWhiteSpace(organization.Email))
            {
                node["email"] = organization.Email;
            }

            website["publisher"] = new JsonObject { ["@id"] = origin + "/#organization" };
            graph.Add(node);
        }

        return new JsonObject { ["@context"] = "https://schema.org", ["@graph"] = graph }.ToJsonString(Options);
    }

    public static string? Build(PublicContentResponse content, string baseUrl, string organizationName)
    {
        ArgumentNullException.ThrowIfNull(content);
        var types = content.Seo.StructuredData;
        if (types.Count == 0)
        {
            return null;
        }

        var url = SitePaths.Absolute(baseUrl, SitePaths.Encode(content.Seo.CanonicalPath));
        var graph = new JsonArray();

        if (types.Contains("WebPage"))
        {
            var page = new JsonObject
            {
                ["@type"] = "WebPage",
                ["@id"] = url,
                ["url"] = url,
                ["name"] = content.Seo.Title,
                ["inLanguage"] = content.LanguageCode,
            };
            if (!string.IsNullOrEmpty(content.Seo.Description))
            {
                page["description"] = content.Seo.Description;
            }

            graph.Add(page);
        }

        if (types.Contains("BreadcrumbList") && content.Breadcrumbs.Count > 1)
        {
            var items = new JsonArray();
            for (var index = 0; index < content.Breadcrumbs.Count; index++)
            {
                var crumb = content.Breadcrumbs[index];
                items.Add(new JsonObject
                {
                    ["@type"] = "ListItem",
                    ["position"] = index + 1,
                    ["name"] = crumb.Title,
                    ["item"] = SitePaths.Absolute(baseUrl, SitePaths.Encode(crumb.Path)),
                });
            }

            graph.Add(new JsonObject { ["@type"] = "BreadcrumbList", ["itemListElement"] = items });
        }

        if (types.Contains("Service"))
        {
            var service = new JsonObject
            {
                ["@type"] = "Service",
                ["name"] = content.Title,
                ["url"] = url,
                ["provider"] = new JsonObject { ["@type"] = "Organization", ["name"] = organizationName },
            };
            if (!string.IsNullOrEmpty(content.Summary))
            {
                service["description"] = content.Summary;
            }

            graph.Add(service);
        }

        if (types.Contains("FaqPage") && content.Faqs.Count > 0)
        {
            var questions = new JsonArray();
            foreach (var faq in content.Faqs)
            {
                questions.Add(new JsonObject
                {
                    ["@type"] = "Question",
                    ["name"] = faq.Question,
                    ["acceptedAnswer"] = new JsonObject { ["@type"] = "Answer", ["text"] = faq.Answer },
                });
            }

            graph.Add(new JsonObject { ["@type"] = "FAQPage", ["mainEntity"] = questions });
        }

        if (graph.Count == 0)
        {
            return null;
        }

        var document = new JsonObject { ["@context"] = "https://schema.org", ["@graph"] = graph };
        return document.ToJsonString(Options);
    }
}
