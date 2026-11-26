using System.Net;
using System.Text.Json;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Media;
using Onyx.Contracts.SiteContent;
using Onyx.Web.Content.SiteContent;
using Onyx.Web.Content.SitePages;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>
/// Admin-managed overrides of the code-owned public site: published documents change text, visibility and contact details;
/// drafts are visible only in the authenticated preview; an unavailable API falls back to the code-owned content.
/// </summary>
public sealed class SiteContentSiteTests : IAsyncLifetime
{
    private static readonly SitePageCatalog Catalog = SitePageCatalog.LoadEmbedded();

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

    // ------------------------------------------------------------------------------------------------ Public site ---

    [Fact]
    public async Task A_published_page_override_changes_text_and_hides_sections_without_touching_the_url()
    {
        var page = Catalog.Find("services.cloud")!;
        var english = page.Translations["en"];
        var hidden = english.Sections.Select((section, index) => (section, index)).First(entry => entry.section.Heading is not null);
        var hiddenKey = SitePageOverlay.SectionKey(hidden.section, hidden.index);
        PublishedDocuments(new Dictionary<string, string>
        {
            [SiteContentKeys.Page(page.Key)] = Json(new PageOverride
            {
                Translations = new(StringComparer.Ordinal) { ["en"] = new PageTextOverride { H1 = "Sample edited cloud heading", Title = "Sample edited title" } },
                Settings = new PageOverrideSettings { HiddenSections = [hiddenKey] },
            }),
        });

        var path = Catalog.PathFor(page.Key, "en")!;
        var html = await _browser.GetHtmlAsync(path);
        var arabic = await _browser.GetHtmlAsync(Catalog.PathFor(page.Key, "ar")!);

        Assert.Contains("Sample edited cloud heading", html, StringComparison.Ordinal);
        Assert.Contains("<title>Sample edited title", html, StringComparison.Ordinal);
        Assert.DoesNotContain(WebUtility.HtmlEncode(hidden.section.Heading!), html, StringComparison.Ordinal);
        Assert.Contains($"<link rel=\"canonical\" href=\"{OnyxWebFactory.PublicBaseUrl}{path}\">", html, StringComparison.Ordinal);
        // Arabic was not overridden and keeps its code-owned heading.
        Assert.Contains(WebUtility.HtmlEncode(page.Translations["ar"].H1), arabic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Published_homepage_copy_replaces_the_resource_text_in_that_language_only()
    {
        PublishedDocuments(new Dictionary<string, string>
        {
            [SiteContentKeys.Home] = Json(new CopyOverride
            {
                Translations = new(StringComparer.Ordinal) { ["en"] = new(StringComparer.Ordinal) { ["Home.Hero.Line1"] = "Sample edited hero line" } },
            }),
        });

        var english = await _browser.GetHtmlAsync("/en/");
        var arabic = await _browser.GetHtmlAsync("/ar/");

        Assert.Contains("Sample edited hero line", english, StringComparison.Ordinal);
        Assert.DoesNotContain("Sample edited hero line", arabic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hidden_homepage_sections_are_not_rendered()
    {
        PublishedDocuments(new Dictionary<string, string>
        {
            [SiteContentKeys.Home] = Json(new CopyOverride { Settings = new CopyOverrideSettings { HiddenSections = ["stack"] } }),
        });

        var html = await _browser.GetHtmlAsync("/en/");

        Assert.DoesNotContain("stack-title", html, StringComparison.Ordinal);
        Assert.Contains("process-title", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Website_settings_appear_in_the_footer_and_replace_the_fallback_offices()
    {
        PublishedDocuments(new Dictionary<string, string>
        {
            [SiteContentKeys.Settings] = Json(new SiteSettingsContent
            {
                ContactEmail = "hello@example.test",
                Offices = [new SiteOffice { Name = new Dictionary<string, string> { ["en"] = "Sample office" } }],
                SocialLinks = [new SiteSocialLink { Network = "linkedin", Url = "https://www.example.test/onyx" }],
            }),
        });

        var html = await _browser.GetHtmlAsync("/en/");

        Assert.Contains("href=\"mailto:hello@example.test\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"https://www.example.test/onyx\"", html, StringComparison.Ordinal);
        Assert.Contains("Sample office", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_settings_the_footer_keeps_the_verified_offices()
    {
        PublishedDocuments(new Dictionary<string, string>());

        var html = await _browser.GetHtmlAsync("/en/");

        Assert.Contains("Riyadh — Granada District, Saudi Arabia", html, StringComparison.Ordinal);
        Assert.Contains("Amman, Jordan", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unavailable_api_falls_back_to_the_code_owned_content()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/site-content", _ => FakeOnyxApi.Problem(HttpStatusCode.ServiceUnavailable, "Unavailable"));
        var page = Catalog.Find("services.cloud")!;

        using var response = await _browser.GetPathAsync(Catalog.PathFor(page.Key, "en")!);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(WebUtility.HtmlEncode(page.Translations["en"].H1), html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_published_json_is_ignored()
    {
        PublishedDocuments(new Dictionary<string, string> { [SiteContentKeys.Home] = "{\"translations\": 5}" });

        using var response = await _browser.GetPathAsync("/en/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------------------------------------------ Admin ---

    [Fact]
    public async Task The_homepage_editor_shows_both_languages_with_the_current_text()
    {
        await SignInAsync(Permissions.Pages.View, Permissions.Pages.Edit);
        AdminDocument(SiteContentKeys.Home, Document(SiteContentKeys.Home));

        var html = await _browser.GetHtmlAsync("/admin/site-content/home");

        Assert.Contains("name=\"en|Home.Hero.Line1\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ar|Home.Hero.Line1\"", html, StringComparison.Ordinal);
        Assert.Contains("dir=\"rtl\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"set|hidden\" value=\"stack\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/site-content/home/preview?lang=ar\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_the_homepage_editor_sends_only_changed_text_as_a_draft()
    {
        var tokens = await SignInAsync(Permissions.Pages.View, Permissions.Pages.Edit);
        AdminDocument(SiteContentKeys.Home, Document(SiteContentKeys.Home));
        _factory.Api.On(HttpMethod.Put, "api/v1/admin/site-content/home/draft", _ => FakeOnyxApi.Json(Document(SiteContentKeys.Home, draft: "{}")));

        using var response = await _browser.SubmitFormAsync("/admin/site-content/home", "/admin/site-content/home", new Dictionary<string, string>
        {
            ["en|Home.Hero.Line1"] = "Sample new hero line",
            ["set|hidden"] = "stack",
            ["version"] = "0",
            ["exists"] = "false",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var sent = Assert.Single(_factory.Api.Requests, request => request.Method == HttpMethod.Put);
        Assert.Equal(tokens.AccessToken, sent.BearerToken);
        var body = sent.BodyAs<SaveSiteContentDraftRequest>();
        Assert.Null(body.Version);
        var draft = SiteContentSnapshot.TryParse<CopyOverride>(body.Json)!;
        Assert.Equal("Sample new hero line", draft.Translations["en"]["Home.Hero.Line1"]);
        Assert.Single(draft.Translations["en"]);
        Assert.Empty(draft.Translations["ar"]);
        Assert.Equal(["stack"], draft.Settings.HiddenSections);
    }

    [Fact]
    public async Task Saving_a_page_sends_structure_free_overrides_with_the_concurrency_version()
    {
        await SignInAsync(Permissions.Pages.View, Permissions.Pages.Edit);
        var key = SiteContentKeys.Page("services.cloud");
        var escaped = Uri.EscapeDataString(key);
        AdminDocument(escaped, Document(key, published: "{}", version: 4));
        _factory.Api.On(HttpMethod.Put, $"api/v1/admin/site-content/{escaped}/draft", _ => FakeOnyxApi.Json(Document(key, draft: "{}", version: 5)));
        var page = Catalog.Find("services.cloud")!;

        using var response = await _browser.SubmitFormAsync("/admin/site-content/pages/services.cloud", "/admin/site-content/pages/services.cloud", new Dictionary<string, string>
        {
            ["en|h1"] = "Sample page heading",
            ["ar|h1"] = page.Translations["ar"].H1,
            ["set|noIndex"] = "true",
            ["version"] = "4",
            ["exists"] = "true",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var sent = Assert.Single(_factory.Api.Requests, request => request.Method == HttpMethod.Put);
        var body = sent.BodyAs<SaveSiteContentDraftRequest>();
        Assert.Equal(4u, body.Version);
        var draft = SiteContentSnapshot.TryParse<PageOverride>(body.Json)!;
        Assert.Equal("Sample page heading", draft.Translations["en"].H1);
        Assert.Null(draft.Translations["ar"].H1);
        Assert.True(draft.Settings.NoIndex);
        Assert.DoesNotContain("\"path\"", body.Json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_preview_renders_the_draft_on_the_public_template_and_is_never_indexed()
    {
        await SignInAsync(Permissions.Pages.View);
        PublishedDocuments(new Dictionary<string, string>());
        var draft = Json(new CopyOverride
        {
            Translations = new(StringComparer.Ordinal) { ["en"] = new(StringComparer.Ordinal) { ["Home.Hero.Line1"] = "Sample draft hero line" } },
        });
        AdminDocument(SiteContentKeys.Home, Document(SiteContentKeys.Home, draft: draft));

        var preview = await _browser.GetHtmlAsync("/admin/site-content/home/preview?lang=en");
        var live = await _browser.GetHtmlAsync("/en/");

        Assert.Contains("Sample draft hero line", preview, StringComparison.Ordinal);
        Assert.Contains("preview-banner", preview, StringComparison.Ordinal);
        Assert.Contains("noindex", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("Sample draft hero line", live, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Publishing_refreshes_the_public_site_immediately()
    {
        await SignInAsync(Permissions.Pages.View, Permissions.Pages.Publish);
        var published = new Dictionary<string, string>();
        _factory.Api.On(HttpMethod.Get, "api/v1/site-content", _ => FakeOnyxApi.Json(new PublicSiteContentResponse(new Dictionary<string, string>(published))));
        await _browser.GetHtmlAsync("/en/");
        AdminDocument(SiteContentKeys.Home, Document(SiteContentKeys.Home, draft: "{}", version: 2));
        _factory.Api.On(HttpMethod.Post, "api/v1/admin/site-content/home/publish", _ =>
        {
            published[SiteContentKeys.Home] = Json(new CopyOverride
            {
                Translations = new(StringComparer.Ordinal) { ["en"] = new(StringComparer.Ordinal) { ["Home.Hero.Line1"] = "Sample published hero line" } },
            });
            return FakeOnyxApi.Json(Document(SiteContentKeys.Home, published: published[SiteContentKeys.Home], version: 3));
        });

        using var response = await _browser.SubmitFormAsync("/admin/site-content/home", "/admin/site-content/publish", new Dictionary<string, string>
        {
            ["key"] = SiteContentKeys.Home,
            ["version"] = "2",
            ["returnUrl"] = "/admin/site-content/home",
        });
        var html = await _browser.GetHtmlAsync("/en/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/site-content/home", response.Location());
        Assert.Equal(2u, Assert.Single(_factory.Api.Requests, request => request.Method == HttpMethod.Post && request.Path.EndsWith("/publish", StringComparison.Ordinal)).BodyAs<SiteContentVersionRequest>().Version);
        Assert.Contains("Sample published hero line", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Return_urls_outside_the_admin_are_ignored()
    {
        await SignInAsync(Permissions.Pages.View, Permissions.Pages.Edit);
        AdminDocument(SiteContentKeys.Home, Document(SiteContentKeys.Home, draft: "{}", version: 2));
        _factory.Api.On(HttpMethod.Post, "api/v1/admin/site-content/home/discard", _ => FakeOnyxApi.Json(Document(SiteContentKeys.Home)));

        using var response = await _browser.SubmitFormAsync("/admin/site-content/home", "/admin/site-content/discard", new Dictionary<string, string>
        {
            ["key"] = SiteContentKeys.Home,
            ["version"] = "2",
            ["returnUrl"] = "https://evil.example.test/",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/site-content", response.Location());
    }

    [Fact]
    public async Task The_settings_editor_prefills_only_the_verified_offices_and_saves_through_the_settings_endpoint()
    {
        await SignInAsync(Permissions.Settings.Manage);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/site-settings", _ => FakeOnyxApi.Json(Document(SiteContentKeys.Settings)));
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/site-settings/revisions", _ => FakeOnyxApi.Json(new List<SiteContentRevisionResponse>()));
        _factory.Api.On(HttpMethod.Put, "api/v1/admin/site-settings/draft", _ => FakeOnyxApi.Json(Document(SiteContentKeys.Settings, draft: "{}")));

        var html = await _browser.GetHtmlAsync("/admin/site-settings");
        using var response = await _browser.SubmitFormAsync("/admin/site-settings", "/admin/site-settings", new Dictionary<string, string>
        {
            ["contactEmail"] = "hello@example.test",
            ["office|0|name|en"] = "Riyadh — Granada District, Saudi Arabia",
            ["social|0|network"] = "linkedin",
            ["social|0|url"] = "https://www.example.test/onyx",
            ["version"] = "0",
            ["exists"] = "false",
        });

        Assert.Contains("value=\"Riyadh — Granada District, Saudi Arabia\"", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.DoesNotContain("@example.test", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var settings = JsonSerializer.Deserialize<SiteSettingsContent>(
            Assert.Single(_factory.Api.Requests, request => request.Method == HttpMethod.Put).BodyAs<SaveSiteContentDraftRequest>().Json, SiteContentSnapshot.Json)!;
        Assert.Equal("hello@example.test", settings.ContactEmail);
        Assert.Equal("https://www.example.test/onyx", Assert.Single(settings.SocialLinks).Url);
        Assert.Single(settings.Offices);
        Assert.Empty(settings.Phones);
    }

    [Fact]
    public async Task The_menu_groups_website_editing_and_hides_settings_without_its_permission()
    {
        await SignInAsync(Permissions.Pages.View);
        AdminDocument(SiteContentKeys.Home, Document(SiteContentKeys.Home));

        var html = await _browser.GetHtmlAsync("/admin/site-content/home");

        Assert.Contains("href=\"/admin/site-content\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin/site-settings\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Api_refusals_on_site_content_become_access_denied()
    {
        await SignInAsync(Permissions.Pages.View);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/site-content/home", _ => FakeOnyxApi.Problem(HttpStatusCode.Forbidden, "Auth.Forbidden"));

        using var response = await _browser.GetPathAsync("/admin/site-content/home");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/account/access-denied", response.Location(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_editors_require_sign_in()
    {
        using var response = await _browser.GetPathAsync("/admin/site-content/home");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/account/login", response.Location(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_dashboard_shows_live_totals_only_for_permitted_areas()
    {
        await SignInAsync(Permissions.Pages.View, Permissions.Media.View);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/site-content", _ => FakeOnyxApi.Json(new List<SiteContentDocumentSummaryResponse>
        {
            new(SiteContentKeys.Home, true, true, null, null, 1),
            new(SiteContentKeys.Shared, false, true, null, null, 2),
        }));
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/media", _ => FakeOnyxApi.Json(new PagedResult<MediaAssetResponse>([], 1, 25, 37)));

        var html = await _browser.GetHtmlAsync("/admin");

        Assert.Contains(">37<", html, StringComparison.Ordinal);
        Assert.Contains("dash-card--attention", html, StringComparison.Ordinal);
        Assert.DoesNotContain(_factory.Api.Requests, request => request.Path.StartsWith("api/v1/admin/leads", StringComparison.Ordinal) || request.Path.StartsWith("api/v1/admin/content", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------------- Helpers ---

    private void PublishedDocuments(IReadOnlyDictionary<string, string> documents) =>
        _factory.Api.On(HttpMethod.Get, "api/v1/site-content", _ => FakeOnyxApi.Json(new PublicSiteContentResponse(documents)));

    private void AdminDocument(string pathKey, SiteContentDocumentResponse document)
    {
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/site-content/{pathKey}", _ => FakeOnyxApi.Json(document));
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/site-content/{pathKey}/revisions", _ => FakeOnyxApi.Json(new List<SiteContentRevisionResponse>()));
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/media", _ => FakeOnyxApi.Json(new PagedResult<MediaAssetResponse>([], 1, 25, 0)));
    }

    private static SiteContentDocumentResponse Document(string key, string? draft = null, string? published = null, uint version = 0) =>
        new(key, draft, published, draft is null ? null : DateTimeOffset.UtcNow, null, published is null ? null : DateTimeOffset.UtcNow, null, published is null ? 0 : 1, version);

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, SiteContentSnapshot.Json);

    private async Task<Onyx.Contracts.Identity.TokenPairResponse> SignInAsync(params string[] permissions)
    {
        var tokens = AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("en", permissions));
        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return tokens;
    }
}
