using Onyx.Application.Brand;
using Onyx.Application.Content;
using Onyx.Application.Email;
using Onyx.Application.Intelligence;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;

namespace Onyx.Application.Tests.Content;

public sealed class ContentGovernanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    private static readonly EmailTemplateDescriptor PasswordReset = new(
        "identity.password-reset", ["ar", "en"], ["name", "minutes"], ["minutes"], true,
        _ => new EmailCopy("Reset", "Reset", ["Valid for {minutes} minutes."], "Reset", null));

    [Fact]
    public void Email_copy_must_keep_required_placeholders()
    {
        var copy = new EmailCopy("Reset your password", "Hello {name}", ["Use the button."], "Reset", null);

        Assert.Equal("EmailTemplate.InvalidPlaceholders", EmailCopyRules.Validate(copy, PasswordReset)!.Code);
        Assert.Null(EmailCopyRules.Validate(copy with { Paragraphs = ["The link works for {minutes} minutes."] }, PasswordReset));
    }

    [Theory]
    [InlineData("Hello {token}")]
    [InlineData("Hello {resetLink}")]
    [InlineData("Hello { name }")]
    [InlineData("Hello {{name}}")]
    public void Email_copy_rejects_unknown_placeholders_and_stray_braces(string heading)
    {
        var copy = new EmailCopy("Subject", heading, ["Valid for {minutes} minutes."], "Reset", null);

        Assert.NotNull(EmailCopyRules.Validate(copy, PasswordReset));
    }

    [Fact]
    public void Email_copy_cannot_add_or_drop_the_button()
    {
        var copy = new EmailCopy("Subject", "Heading", ["Valid for {minutes} minutes."], null, null);

        Assert.NotNull(EmailCopyRules.Validate(copy, PasswordReset));
        Assert.NotNull(EmailCopyRules.Validate(copy with { ActionLabel = "Go" }, PasswordReset with { HasAction = false }));
    }

    [Fact]
    public void Placeholders_are_filled_as_plain_values()
    {
        Assert.Equal("Hi <b>, 30 min {unknown}", EmailCopyRules.Fill("Hi {name}, {minutes} min {unknown}", new Dictionary<string, string> { ["name"] = "<b>", ["minutes"] = "30" }));
    }

    [Fact]
    public void Body_images_never_carry_branding_and_missing_logo_means_needs_information()
    {
        var kit = BrandKit.CreateEmpty();

        Assert.Null(BrandedImagePolicy.ForPurpose(ImagePurpose.ArticleBody, kit).Value);
        var featured = BrandedImagePolicy.ForPurpose(ImagePurpose.Featured, kit);
        Assert.True(featured.IsFailure);
        Assert.Equal("Brand.NeedsInformation", featured.Error!.Code);
    }

    [Fact]
    public void Health_is_insufficient_data_without_two_search_console_periods()
    {
        Assert.Equal(ContentHealth.InsufficientData, IntelligenceStatusService.AssessHealth([]).Health);

        var analyticsOnly = Snapshot(EvidenceSource.WebAnalytics, 28, 100);
        var oneConsolePeriod = Snapshot(EvidenceSource.SearchConsole, 28, 100);
        Assert.Equal(ContentHealth.InsufficientData, IntelligenceStatusService.AssessHealth([analyticsOnly, oneConsolePeriod]).Health);
    }

    [Fact]
    public void A_decline_is_reported_as_an_observation_with_its_source_and_periods()
    {
        var (health, reasons) = IntelligenceStatusService.AssessHealth([Snapshot(EvidenceSource.SearchConsole, 56, 100), Snapshot(EvidenceSource.SearchConsole, 28, 40)]);

        Assert.Equal(ContentHealth.Declining, health);
        Assert.Contains("Search Console", Assert.Single(reasons), StringComparison.Ordinal);
    }

    [Fact]
    public void Revision_comparison_lists_changed_fields_per_language()
    {
        var before = Snapshot("About", "about");
        var after = Snapshot("About Onyx", "about");

        var differences = ContentRevisionService.Compare(before, after);

        var title = Assert.Single(differences);
        Assert.Equal("en", title.LanguageCode);
        Assert.Equal("About", title.Before);
        Assert.Equal("About Onyx", title.After);
        Assert.Empty(ContentRevisionService.Compare(before, before));
    }

    [Fact]
    public void Structured_data_is_limited_to_what_the_page_shows()
    {
        var all = StructuredDataTypes.WebPage | StructuredDataTypes.BreadcrumbList | StructuredDataTypes.Service | StructuredDataTypes.FaqPage;

        var page = PublicContentBuilder.SupportedStructuredData(ContentType.Page, all, faqCount: 0, breadcrumbCount: 1);
        var service = PublicContentBuilder.SupportedStructuredData(ContentType.Service, all, faqCount: 2, breadcrumbCount: 3);

        Assert.Equal(["WebPage"], page);
        Assert.Equal(["WebPage", "BreadcrumbList", "Service", "FaqPage"], service);
    }

    [Fact]
    public void Preview_tokens_are_random_and_stored_only_as_hashes()
    {
        var token = PreviewTokenCodec.NewToken();

        Assert.True(PreviewTokenCodec.IsWellFormed(token));
        Assert.NotEqual(token, PreviewTokenCodec.NewToken());
        Assert.Equal(64, PreviewTokenCodec.Hash(token).Length);
        Assert.DoesNotContain(token, PreviewTokenCodec.Hash(token), StringComparison.Ordinal);
        Assert.False(PreviewTokenCodec.IsWellFormed("short"));
        Assert.False(PreviewTokenCodec.IsWellFormed(null));
    }

    private static ContentPerformanceSnapshot Snapshot(EvidenceSource source, int daysAgo, double clicks) =>
        ContentPerformanceSnapshot.Collect(
            Guid.NewGuid(), "en", source, Now.AddDays(-daysAgo), Now.AddDays(-daysAgo + 27), new Dictionary<string, double> { ["clicks"] = clicks }, Now).Value;

    private static ContentSnapshot Snapshot(string title, string slug) =>
        new(ContentType.Page, null, null, ContentPresentation.Page, 0, null, null,
            [new TranslationSnapshot("en", title, "Summary", slug, [], [], new SeoProfile())]);
}
