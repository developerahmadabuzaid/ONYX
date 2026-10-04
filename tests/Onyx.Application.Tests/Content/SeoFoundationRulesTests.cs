using Onyx.Application.Content;
using Onyx.Domain.Content;

namespace Onyx.Application.Tests.Content;

public sealed class SeoFoundationRulesTests
{
    [Fact]
    public void The_fingerprint_ignores_ordering_and_keys_but_not_visible_content()
    {
        var page = Snapshot("About", "about");

        Assert.Equal(PublicPageFingerprint.For(page, "en"), PublicPageFingerprint.For(page with { SortOrder = 9, Key = "about" }, "en"));
        Assert.NotEqual(PublicPageFingerprint.For(page, "en"), PublicPageFingerprint.For(Snapshot("About Onyx", "about"), "en"));
        Assert.NotEqual(PublicPageFingerprint.For(page, "en"), PublicPageFingerprint.For(Snapshot("About", "about-onyx"), "en"));
        Assert.NotEqual(PublicPageFingerprint.For(page, "en"), PublicPageFingerprint.For(page with { FeaturedImageId = Guid.NewGuid() }, "en"));
    }

    public static TheoryData<string, SeoProfile> InternalSeoChanges => new()
    {
        { "primary keyword", new SeoProfile(PrimaryKeyword: "cloud migration") },
        { "secondary keywords", new SeoProfile(SecondaryKeywords: ["cloud", "migration"]) },
        { "search intent", new SeoProfile(SearchIntent: SearchIntent.Commercial) },
        { "target market", new SeoProfile(TargetMarket: "SA") },
        { "SEO status", new SeoProfile(Status: SeoStatus.Ready) },
        { "SEO notes", new SeoProfile(Notes: "Check the title next week.") },
    };

    public static TheoryData<string, SeoProfile> PublicSeoChanges => new()
    {
        { "meta title", new SeoProfile(MetaTitle: "About Onyx") },
        { "meta description", new SeoProfile(MetaDescription: "Who we are and how we work.") },
        { "canonical", new SeoProfile(CanonicalPath: "/en/elsewhere") },
        { "index", new SeoProfile(Index: false) },
        { "follow", new SeoProfile(Follow: false) },
        { "structured data", new SeoProfile(StructuredData: StructuredDataTypes.WebPage) },
        { "Open Graph title", new SeoProfile(OpenGraphTitle: "About") },
        { "Open Graph description", new SeoProfile(OpenGraphDescription: "Who we are.") },
        { "Open Graph image", new SeoProfile(OpenGraphImageId: Guid.Parse("0192f7c4-0000-7000-8000-000000000009")) },
    };

    [Theory]
    [MemberData(nameof(InternalSeoChanges))]
    public void Internal_seo_planning_fields_do_not_change_the_fingerprint(string field, SeoProfile changed)
    {
        Assert.True(PublicPageFingerprint.For(Snapshot("About", "about"), "en") == PublicPageFingerprint.For(Snapshot("About", "about", changed), "en"), field);
    }

    [Theory]
    [MemberData(nameof(PublicSeoChanges))]
    public void Served_seo_metadata_changes_the_fingerprint(string field, SeoProfile changed)
    {
        Assert.True(PublicPageFingerprint.For(Snapshot("About", "about"), "en") != PublicPageFingerprint.For(Snapshot("About", "about", changed), "en"), field);
    }

    [Fact]
    public void Visible_content_and_presentation_changes_change_the_fingerprint()
    {
        var page = Snapshot("About", "about");
        var english = page.Translations[0];
        var blocks = new List<ContentBlock> { new(ContentBlockType.Paragraph, Spans: [new TextSpan("New text")]) };

        Assert.NotEqual(PublicPageFingerprint.For(page, "en"), PublicPageFingerprint.For(page with { Translations = [english with { Summary = "Changed" }, page.Translations[1]] }, "en"));
        Assert.NotEqual(PublicPageFingerprint.For(page, "en"), PublicPageFingerprint.For(page with { Translations = [english with { Body = blocks }, page.Translations[1]] }, "en"));
        Assert.NotEqual(PublicPageFingerprint.For(page, "en"), PublicPageFingerprint.For(page with { Translations = [english with { Faqs = [new FaqEntry("Q?", "A.")] }, page.Translations[1]] }, "en"));
        Assert.NotEqual(PublicPageFingerprint.For(page, "en"), PublicPageFingerprint.For(page with { Presentation = ContentPresentation.TaxonomyOnly }, "en"));
        Assert.NotEqual(PublicPageFingerprint.For(page, "en"), PublicPageFingerprint.For(page with { PrimaryCtaId = Guid.NewGuid() }, "en"));
        Assert.NotEqual(PublicPageFingerprint.For(page, "en"), PublicPageFingerprint.For(page with { ParentId = Guid.NewGuid() }, "en"));
        Assert.NotEqual(PublicPageFingerprint.For(page, "en"), PublicPageFingerprint.For(page with { Type = ContentType.Service }, "en"));
    }

    [Fact]
    public void Identical_snapshots_have_identical_fingerprints() =>
        Assert.Equal(PublicPageFingerprint.For(Snapshot("About", "about"), "en"), PublicPageFingerprint.For(Snapshot("About", "about"), "en"));

    [Fact]
    public void A_change_in_one_language_does_not_change_the_other_languages_fingerprint()
    {
        var before = Snapshot("About", "about");
        var after = before with { Translations = [before.Translations[0] with { Title = "Changed" }, before.Translations[1]] };

        Assert.NotEqual(PublicPageFingerprint.For(before, "en"), PublicPageFingerprint.For(after, "en"));
        Assert.Equal(PublicPageFingerprint.For(before, "ar"), PublicPageFingerprint.For(after, "ar"));
    }

    [Theory]
    [InlineData(true, null, true)]
    [InlineData(false, null, false)]
    [InlineData(true, "/en/elsewhere", false)]
    public void Only_indexable_self_canonical_pages_take_part_in_hreflang(bool index, string? canonical, bool eligible)
    {
        var snapshot = Snapshot("About", "about", new SeoProfile(Index: index, CanonicalPath: canonical));

        Assert.Equal(eligible, PublicContentBuilder.IsHreflangEligible(snapshot, "en"));
        Assert.False(PublicContentBuilder.IsHreflangEligible(snapshot, "fr"));
    }

    private static ContentSnapshot Snapshot(string title, string slug, SeoProfile? seo = null) =>
        new(ContentType.Page, null, null, ContentPresentation.Page, 0, null, null,
        [
            new TranslationSnapshot("en", title, "Summary", slug, [], [], seo ?? new SeoProfile()),
            new TranslationSnapshot("ar", "من نحن", "ملخص", "من-نحن", [], [], new SeoProfile()),
        ]);
}
