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
