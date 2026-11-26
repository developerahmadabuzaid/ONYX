using System.Text.RegularExpressions;
using Onyx.Contracts.Content;
using Onyx.Web.Content.SitePages;

namespace Onyx.Web.Tests;

/// <summary>Content rules for the code-owned site pages: complete bilingual copy, unique SEO metadata, resolvable links, clean slugs.</summary>
public sealed partial class SitePageCatalogTests
{
    private static readonly SitePageCatalog Catalog = SitePageCatalog.LoadEmbedded();
    private static readonly string[] Languages = ["ar", "en"];
    private static readonly HashSet<string> CodeRouteKeys = new(StringComparer.Ordinal) { "lead.contact", "lead.start-project", "lead.book-consultation" };

    [Fact]
    public void The_catalog_contains_the_complete_public_site()
    {
        string[] expected =
        [
            "services", "services.ai", "services.ai-agents", "services.ai-as-service", "services.software", "services.cloud", "services.security", "services.growth",
            "about", "how-we-work", "technologies", "idea-to-product", "legal.privacy", "legal.terms", "legal.cookies",
        ];

        Assert.Equal(expected.Order(StringComparer.Ordinal), Catalog.Pages.Select(page => page.Key).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_page_is_complete_and_structurally_identical_in_both_languages()
    {
        foreach (var page in Catalog.Pages)
        {
            Assert.All(Languages, language => Assert.True(page.Translations.ContainsKey(language), $"{page.Key} lacks {language}"));
            var ar = page.Translations["ar"];
            var en = page.Translations["en"];
            foreach (var text in new[] { ar, en })
            {
                Assert.False(string.IsNullOrWhiteSpace(text.Title), page.Key);
                Assert.False(string.IsNullOrWhiteSpace(text.Description), page.Key);
                Assert.False(string.IsNullOrWhiteSpace(text.H1), page.Key);
                Assert.False(string.IsNullOrWhiteSpace(text.Nav), page.Key);
            }

            Assert.Equal(en.Sections.Select(s => (s.Kind, s.Id, s.Items.Count)), ar.Sections.Select(s => (s.Kind, s.Id, s.Items.Count)));
            Assert.Equal(
                en.Sections.SelectMany(s => s.Items).Select(i => (i.Id, i.Page, i.Flag, i.Tags.Count, i.Pending.Count)),
                ar.Sections.SelectMany(s => s.Items).Select(i => (i.Id, i.Page, i.Flag, i.Tags.Count, i.Pending.Count)));
        }
    }

    [Fact]
    public void Titles_and_descriptions_are_unique_and_fit_search_results()
    {
        foreach (var language in Languages)
        {
            var texts = Catalog.Pages.Select(page => page.Translations[language]).ToList();
            Assert.Equal(texts.Count, texts.Select(t => t.Title).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(texts.Count, texts.Select(t => t.Description).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(texts.Count, texts.Select(t => t.H1).Distinct(StringComparer.Ordinal).Count());
            Assert.All(texts, text => Assert.InRange(text.Title.Length, 10, 65));
            Assert.All(texts, text => Assert.InRange(text.Description.Length, 60, 230));
        }
    }

    [Fact]
    public void Links_between_pages_always_resolve()
    {
        foreach (var page in Catalog.Pages)
        {
            Assert.True(page.Parent is null || Catalog.Find(page.Parent) is not null, page.Key);
            Assert.All(page.Related, key => Assert.NotNull(Catalog.Find(key)));
            var linked = page.Translations.Values.SelectMany(t => t.Sections).SelectMany(s => s.Items).Select(i => i.Page).OfType<string>();
            Assert.All(linked, key => Assert.True(Catalog.Find(key) is not null || CodeRouteKeys.Contains(key), $"{page.Key} links to unknown '{key}'"));
        }
    }

    [Fact]
    public void Anchor_ids_are_unique_within_each_page()
    {
        foreach (var text in Catalog.Pages.SelectMany(page => page.Translations.Values))
        {
            var ids = text.Sections.Select(s => s.Id).Concat(text.Sections.SelectMany(s => s.Items).Select(i => i.Id)).OfType<string>().ToList();
            Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void Indexable_pages_meet_a_content_minimum()
    {
        foreach (var page in Catalog.Pages.Where(page => !page.Draft))
        {
            var english = page.Translations["en"];
            var words = english.Sections
                .SelectMany(s => s.Paragraphs.Append(s.Lead ?? string.Empty).Concat(s.Items.SelectMany(i => i.Points.Concat(i.Tags).Concat(i.Deliverables).Concat(i.Involvement).Append(i.Text ?? string.Empty).Append(i.Title))))
                .Sum(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.True(words >= 180, $"{page.Key} has only {words} words of body copy");
        }
    }

    [Fact]
    public void Paths_are_clean_localized_slugs_reserved_from_the_cms()
    {
        foreach (var page in Catalog.Pages)
        {
            Assert.Matches(EnglishPath(), page.Translations["en"].Path);
            Assert.Matches(ArabicPath(), page.Translations["ar"].Path);
            foreach (var text in page.Translations.Values)
            {
                Assert.Contains(text.Path.Split('/')[0], ContentRouteSegments.Reserved);
                Assert.All(text.Path.Split('/'), segment => Assert.True(segment.Length <= 60, segment));
            }
        }
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*(/[a-z0-9]+(-[a-z0-9]+)*)*$")]
    private static partial Regex EnglishPath();

    // Arabic letters (no tashkeel or tatweel), Western digits and hyphens.
    [GeneratedRegex("^[ء-غف-يa-z0-9]+(-[ء-غف-يa-z0-9]+)*(/[ء-غف-يa-z0-9]+(-[ء-غف-يa-z0-9]+)*)*$")]
    private static partial Regex ArabicPath();
}
