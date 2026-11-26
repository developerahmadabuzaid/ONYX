using Onyx.Application.SiteContent;
using Onyx.Contracts.SiteContent;

namespace Onyx.Application.Tests.SiteContent;

public sealed class SiteContentRulesTests
{
    [Fact]
    public void Plain_text_documents_are_accepted_and_media_references_collected()
    {
        var mediaId = Guid.NewGuid();
        var json = """{"translations":{"ar":{"h1":"عنوان"}},"settings":{"heroImageMediaId":"MEDIA","hiddenSections":["faq"]}}""".Replace("MEDIA", mediaId.ToString(), StringComparison.Ordinal);
        var error = SiteContentJson.Validate(json, out var media);

        Assert.Null(error);
        Assert.Equal([mediaId], media);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("{not json")]
    [InlineData("{\"h1\":\"<script>alert(1)</script>\"}")]
    [InlineData("{\"h1\":\"a > b\"}")]
    [InlineData("{\"h1\":\"line\\u0000break\"}")]
    [InlineData("{\"settings\":{\"heroImageMediaId\":\"not-a-guid\"}}")]
    public void Markup_malformed_json_and_invalid_media_ids_are_rejected(string json) =>
        Assert.NotNull(SiteContentJson.Validate(json, out _));

    [Fact]
    public void Deeply_nested_and_oversized_strings_are_rejected()
    {
        var deep = string.Concat(Enumerable.Repeat("{\"a\":", SiteContentJson.MaxDepth + 2)) + "1" + new string('}', SiteContentJson.MaxDepth + 2);
        var longText = $$"""{"h1":"{{new string('a', SiteContentJson.MaxStringLength + 1)}}"}""";

        Assert.NotNull(SiteContentJson.Validate(deep, out _));
        Assert.NotNull(SiteContentJson.Validate(longText, out _));
    }

    [Fact]
    public void Settings_accept_real_contact_details()
    {
        var result = new SiteSettingsContentValidator().Validate(new SiteSettingsContent
        {
            CompanyName = new Dictionary<string, string> { ["ar"] = "أونيكس", ["en"] = "Onyx" },
            ContactEmail = "hello@example.test",
            Phones = [new SitePhone { Number = "+966 11 000 0000", Label = new Dictionary<string, string> { ["en"] = "Office" } }],
            Offices = [new SiteOffice { Name = new Dictionary<string, string> { ["en"] = "Riyadh" } }],
            SocialLinks = [new SiteSocialLink { Network = "linkedin", Url = "https://www.example.test/onyx" }],
        });

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("not-an-email", null, null, null)]
    [InlineData(null, "call me", null, null)]
    [InlineData(null, null, "myspace", "https://www.example.test/onyx")]
    [InlineData(null, null, "linkedin", "http://www.example.test/onyx")]
    [InlineData(null, null, "linkedin", "javascript:alert(1)")]
    public void Settings_reject_invalid_contact_details(string? email, string? phone, string? network, string? url)
    {
        var result = new SiteSettingsContentValidator().Validate(new SiteSettingsContent
        {
            ContactEmail = email,
            Phones = phone is null ? [] : [new SitePhone { Number = phone }],
            SocialLinks = network is null ? [] : [new SiteSocialLink { Network = network, Url = url! }],
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Settings_reject_unconfigured_languages()
    {
        var result = new SiteSettingsContentValidator().Validate(new SiteSettingsContent { CompanyName = new Dictionary<string, string> { ["fr"] = "Onyx" } });

        Assert.False(result.IsValid);
    }
}
