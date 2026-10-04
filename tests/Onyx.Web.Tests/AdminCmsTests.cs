using System.Net;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

public sealed class AdminCmsTests : IAsyncLifetime
{
    private static readonly Guid ContentId = Guid.CreateVersion7();

    private readonly OnyxWebFactory _factory = new();
    private HttpClient _browser = null!;

    public ValueTask InitializeAsync()
    {
        _browser = _factory.CreateBrowser();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _browser.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task The_menu_shows_cms_sections_only_with_their_permission()
    {
        var tokens = await SignInAsync(Permissions.Content.View);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/content", _ => FakeOnyxApi.Json(new PagedResult<ContentSummaryResponse>([Summary()], 1, 25, 1)));

        var html = await _browser.GetHtmlAsync("/admin/content");

        Assert.Contains("href=\"/admin/content\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin/brand-kit\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin/email-templates\"", html, StringComparison.Ordinal);
        Assert.Contains("Sample page", html, StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.AccessToken, html, StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.RefreshToken, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Body_text_is_sent_to_the_api_as_structured_blocks_with_the_bearer_token_attached_server_side()
    {
        var tokens = await SignInAsync(Permissions.Content.View, Permissions.Content.Edit);
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/content/{ContentId:D}", _ => FakeOnyxApi.Json(Detail()));
        _factory.Api.On(HttpMethod.Put, $"api/v1/admin/content/{ContentId:D}/translations/en", _ => FakeOnyxApi.Json(Detail()));

        using var response = await _browser.SubmitFormAsync($"/admin/content/{ContentId:D}/translations/en", $"/admin/content/{ContentId:D}/translations/en", new Dictionary<string, string>
        {
            ["Title"] = "Sample page",
            ["Summary"] = "Summary",
            ["Slug"] = "sample-page",
            ["BodyText"] = "## Heading\n\nText with **bold** and <b>tags</b>.",
            ["FaqText"] = "Q: Question?\nA: Answer.",
            ["Version"] = "7",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var sent = Assert.Single(_factory.Api.Requests, request => request.Method == HttpMethod.Put);
        Assert.Equal(tokens.AccessToken, sent.BearerToken);
        var body = sent.BodyAs<UpdateContentTranslationRequest>();
        Assert.Equal(["Heading", "Paragraph"], body.Body.Select(block => block.Type));
        Assert.Contains(body.Body[1].Spans!, span => span.Text == "bold" && span.Emphasis == "Strong");
        Assert.Contains(body.Body[1].Spans!, span => span.Text.Contains("<b>tags</b>", StringComparison.Ordinal));
        Assert.Equal(7u, body.Version);
        Assert.Equal("Question?", Assert.Single(body.Faqs).Question);
    }

    [Fact]
    public async Task Invalid_body_markup_is_reported_without_calling_the_api_and_keeps_the_text()
    {
        await SignInAsync(Permissions.Content.View, Permissions.Content.Edit);
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/content/{ContentId:D}", _ => FakeOnyxApi.Json(Detail()));

        using var response = await _browser.SubmitFormAsync($"/admin/content/{ContentId:D}/translations/en", $"/admin/content/{ContentId:D}/translations/en", new Dictionary<string, string>
        {
            ["Title"] = "Sample page",
            ["Slug"] = "sample-page",
            ["BodyText"] = "Intro\n\n[click](javascript:alert(1))",
            ["Version"] = "7",
        });
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(_factory.Api.Requests, request => request.Method == HttpMethod.Put);
        Assert.Contains("(line 3)", html, StringComparison.Ordinal);
        Assert.Contains("[click](javascript:alert(1))", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Api_permission_refusals_become_access_denied()
    {
        await SignInAsync(Permissions.Content.View);
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/content/{ContentId:D}", _ => FakeOnyxApi.Problem(HttpStatusCode.Forbidden, "Content.TypeForbidden"));

        using var response = await _browser.GetPathAsync($"/admin/content/{ContentId:D}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/account/access-denied", response.Location(), StringComparison.Ordinal);
    }

    private async Task<Onyx.Contracts.Identity.TokenPairResponse> SignInAsync(params string[] permissions)
    {
        var tokens = AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("en", permissions));
        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return tokens;
    }

    private static ContentSummaryResponse Summary() =>
        new(ContentId, "Page", null, null, "Page", "Draft", false, false, new Dictionary<string, string> { ["en"] = "Sample page" }, "None", null, null, null, null);

    private static ContentDetailResponse Detail() =>
        new(ContentId, "Page", null, null, "Page", 0, "Draft", false, false, 3, null, null, null, null, null, null, null, null, null, null, "None", null,
            [new ContentTranslationResponse("en", "Sample page", "Summary", "sample-page", [], [], new SeoProfileDto(null, null, null, null, null, null, true, true, null, null, null, null, null, null, null), true)],
            [], 7);
}
