using Onyx.Domain.Content;

namespace Onyx.Domain.Tests.Content;

public sealed class SlugAndBodyRulesTests
{
    [Theory]
    [InlineData("AI Solutions", "ai-solutions")]
    [InlineData("  Cloud -- & DevOps!  ", "cloud-devops")]
    [InlineData("حلول الذكاء الاصطناعي", "حلول-الذكاء-الاصطناعي")]
    [InlineData("حُلُول الـــذكاء", "حلول-الذكاء")]
    [InlineData("خدمة ٢٠٢٦", "خدمة-2026")]
    [InlineData("", "")]
    public void Slugs_are_normalized(string input, string expected) => Assert.Equal(expected, Slug.Normalize(input));

    [Theory]
    [InlineData("ai-solutions", true)]
    [InlineData("AI-Solutions", false)]
    [InlineData("-leading", false)]
    [InlineData("a/b", false)]
    [InlineData("", false)]
    public void Only_normalized_slugs_are_valid(string slug, bool valid) => Assert.Equal(valid, Slug.IsValid(slug));

    [Fact]
    public void Overlong_slugs_are_invalid() => Assert.False(Slug.IsValid(new string('a', Slug.MaxLength + 1)));

    [Theory]
    [InlineData("/en/services", true)]
    [InlineData("//evil.example", false)]
    [InlineData("https://evil.example", false)]
    [InlineData("/en/../admin", false)]
    [InlineData("relative", false)]
    public void Internal_link_paths_must_be_safe(string path, bool safe) => Assert.Equal(safe, ContentBodyRules.IsSafeRelativePath(path));

    [Theory]
    [InlineData("https://example.org/page", true)]
    [InlineData("http://example.org", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://user:pass@example.org", false)]
    public void External_links_must_be_plain_https(string url, bool safe) => Assert.Equal(safe, ContentBodyRules.IsSafeExternalUrl(url));

    [Fact]
    public void Body_blocks_reject_unsafe_links_and_bad_shapes()
    {
        Assert.Null(ContentBodyRules.Validate([new ContentBlock(ContentBlockType.Paragraph, Spans: [new TextSpan("ok", Link: new LinkTarget(Path: "/en/services"))])]));
        Assert.NotNull(ContentBodyRules.Validate([new ContentBlock(ContentBlockType.Paragraph, Spans: [new TextSpan("x", Link: new LinkTarget(Url: "javascript:alert(1)"))])]));
        Assert.NotNull(ContentBodyRules.Validate([new ContentBlock(ContentBlockType.Heading, Level: 1, Spans: [new TextSpan("H1 is the title")])]));
        Assert.NotNull(ContentBodyRules.Validate([new ContentBlock(ContentBlockType.Image)]));
        Assert.NotNull(ContentBodyRules.Validate([new ContentBlock(ContentBlockType.Paragraph, Spans: [new TextSpan("bad\u0007")])]));
    }

    [Fact]
    public void Faqs_need_a_question_and_an_answer() =>
        Assert.NotNull(ContentBodyRules.ValidateFaqs([new FaqEntry("Question?", " ")]));

    [Fact]
    public void Redirects_never_target_external_sites_or_the_homepage_by_default()
    {
        Assert.True(Redirect.Create("en", "old-page", RedirectKind.Permanent, null, "https://evil.example", null).IsFailure);
        Assert.True(Redirect.Create("en", "old-page", RedirectKind.Permanent, null, null, null).IsFailure);
        Assert.True(Redirect.Create("en", "old-page", RedirectKind.Gone, null, null, null).IsSuccess);
        Assert.True(Redirect.Create("en", "old-page", RedirectKind.Permanent, Guid.NewGuid(), null, null).IsSuccess);
        Assert.True(Redirect.Create("en", "old-page", RedirectKind.Permanent, null, "../admin", null).IsFailure);
        Assert.True(Redirect.Create("en", "../x", RedirectKind.Gone, null, null, null).IsFailure);
        Assert.True(Redirect.Create("en", "old-page", RedirectKind.Permanent, null, "services/new-name", null).IsSuccess);
    }
}
