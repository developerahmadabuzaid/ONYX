using Onyx.Domain.Leads;

namespace Onyx.Domain.Tests.Leads;

public sealed class AttributionTests
{
    private static readonly TouchPoint First = new("/en/services", "https://search.example/results?q=private", "google", "cpc", "launch", null, null, DateTimeOffset.UnixEpoch);
    private static readonly TouchPoint Last = new("/en/contact", null, "newsletter", null, null, null, null, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Without_analytics_consent_only_the_submitting_session_context_is_kept()
    {
        var attribution = Attribution.Create(First, Last, "/en/contact", "en", "sa", "Mobile", "123.456", analyticsConsent: false);

        Assert.Null(attribution.FirstTouch);
        Assert.Null(attribution.AnalyticsClientId);
        Assert.Equal("newsletter", attribution.LastTouch!.UtmSource);
        Assert.Equal("/en/contact", attribution.SubmissionPath);
        Assert.Equal("SA", attribution.Market);
        Assert.Equal("mobile", attribution.DeviceClass);
        Assert.False(attribution.AnalyticsConsent);
    }

    [Fact]
    public void With_analytics_consent_first_touch_and_client_id_are_kept_with_the_referrer_reduced_to_its_origin()
    {
        var attribution = Attribution.Create(First, Last, "/en/contact", "en", null, "desktop", "123.456", analyticsConsent: true);

        Assert.Equal("https://search.example", attribution.FirstTouch!.Referrer);
        Assert.Equal("123.456", attribution.AnalyticsClientId);
    }

    [Theory]
    [InlineData("https://evil.example/landing")]
    [InlineData("//evil.example")]
    [InlineData("relative")]
    public void Landing_and_submission_paths_must_be_site_paths(string path)
    {
        var attribution = Attribution.Create(null, Last with { LandingPath = path }, path, "en", null, null, null, false);

        Assert.Null(attribution.SubmissionPath);
        Assert.Null(attribution.LastTouch!.LandingPath);
    }

    [Fact]
    public void Unknown_device_classes_markets_and_malformed_client_ids_are_dropped()
    {
        var attribution = Attribution.Create(null, null, null, "en", "Saudi", "smart-fridge", "<script>", true);

        Assert.Null(attribution.Market);
        Assert.Null(attribution.DeviceClass);
        Assert.Null(attribution.AnalyticsClientId);
        Assert.Null(attribution.LastTouch);
    }
}
