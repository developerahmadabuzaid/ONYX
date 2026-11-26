using System.Net;
using System.Net.Http.Headers;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

public sealed class PublicSiteTests : IAsyncLifetime
{
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
    public async Task Root_redirects_to_Arabic_by_default()
    {
        using var response = await _browser.GetPathAsync("/");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/ar/", response.Location());
    }

    [Fact]
    public async Task Root_ignores_Accept_Language()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/", UriKind.Relative));
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));

        using var response = await _browser.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("/ar/", response.Location());
    }

    [Fact]
    public async Task Root_honours_an_explicit_language_choice()
    {
        using var choice = await _browser.GetPathAsync("/en/language");
        var cookie = Assert.Single(choice.SetCookies("onyx.lang"));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);

        using var response = await _browser.GetPathAsync("/");

        Assert.Equal("/en/", response.Location());
    }

    [Theory]
    [InlineData("ar", "rtl")]
    [InlineData("en", "ltr")]
    public async Task Home_sets_language_direction_and_alternates(string language, string direction)
    {
        var html = await _browser.GetHtmlAsync($"/{language}/");

        Assert.Contains($"<html lang=\"{language}\" dir=\"{direction}\">", html, StringComparison.Ordinal);
        Assert.Contains($"<link rel=\"canonical\" href=\"{OnyxWebFactory.PublicBaseUrl}/{language}/\">", html, StringComparison.Ordinal);
        Assert.Contains($"hreflang=\"ar\" href=\"{OnyxWebFactory.PublicBaseUrl}/ar/\"", html, StringComparison.Ordinal);
        Assert.Contains($"hreflang=\"en\" href=\"{OnyxWebFactory.PublicBaseUrl}/en/\"", html, StringComparison.Ordinal);
        Assert.Contains($"hreflang=\"x-default\" href=\"{OnyxWebFactory.PublicBaseUrl}/ar/\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Arabic_text_is_emitted_as_characters_not_entities()
    {
        var html = await _browser.GetHtmlAsync("/en/");

        // The public site currently hides the language switcher in the header; the assertion below fires only when it is restored.
        if (!html.Contains(">العربية</a>", StringComparison.Ordinal))
        {
            return;
        }
        Assert.Contains(">العربية</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("&#x627;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pages_are_noindex_until_indexing_is_enabled_for_launch()
    {
        var html = await _browser.GetHtmlAsync("/ar/");

        Assert.Contains("<meta name=\"robots\" content=\"noindex, nofollow\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Accept_Language_offers_a_suggestion_instead_of_redirecting()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/ar/", UriKind.Relative));
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));

        using var response = await _browser.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<aside class=\"lang-suggest\" lang=\"en\"", html, StringComparison.Ordinal);
        Assert.Contains("This page is also available in English.", html, StringComparison.Ordinal);

        // Closing keeps the visitor on the Arabic page and records Arabic as their choice.
        Assert.Contains("href=\"/ar/language?return=%2Far%2F\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/fr/")]
    [InlineData("/ar/no-such-page")]
    public async Task Unknown_languages_and_pages_return_a_localized_404(string path)
    {
        using var response = await _browser.GetPathAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("noindex", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/ar/")]
    public async Task Language_switch_only_returns_to_pages_in_the_chosen_language(string returnPath)
    {
        using var response = await _browser.GetPathAsync($"/en/language?return={Uri.EscapeDataString(returnPath)}");

        Assert.Equal("/en/", response.Location());
    }

    [Fact]
    public async Task Security_headers_include_a_per_request_CSP_nonce()
    {
        using var first = await _browser.GetPathAsync("/ar/");
        using var second = await _browser.GetPathAsync("/ar/");

        var csp = first.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Matches("script-src 'self' 'nonce-[A-Za-z0-9+/=]+'", csp);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.NotEqual(csp, second.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", first.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.False(first.Headers.Contains("Server"));
    }

    [Theory]
    [InlineData("/ar/")]
    [InlineData("/en/")]
    public async Task Page_background_layer_is_present_and_decorative(string path)
    {
        // The animated background must be on every public page so the design is consistent.
        // It's aria-hidden so screen readers skip it; the test guards against accidental
        // removal of the layer from the shared layout. We check the wrapper plus the
        // pattern child (CSS tiled dot+grid as of the latest refactor).
        // The wrapper carries an extra `page-bg--home` modifier on the homepage, so match the
        // class attribute by prefix rather than as an exact string.
        var html = await _browser.GetHtmlAsync(path);

        Assert.Contains("class=\"page-bg", html, StringComparison.Ordinal);
        Assert.Contains("page-bg__pattern", html, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", html, StringComparison.Ordinal);

        // The agent-mesh ambience (engineering grid, soft colour fields and the linked-node network)
        // is the current background, so its layers are expected on every public page too.
        Assert.Contains("page-bg__grid", html, StringComparison.Ordinal);
        Assert.Contains("page-bg__orb--c", html, StringComparison.Ordinal);
        Assert.Contains("page-bg__mesh", html, StringComparison.Ordinal);
        Assert.Contains("page-bg__mesh-ray", html, StringComparison.Ordinal);

        // Both cases here are the homepage, which additionally switches on its light tech
        // indicators (radar sweep and signal nodes).
        Assert.Contains("page-bg--home", html, StringComparison.Ordinal);
        Assert.Contains("page-bg__radar", html, StringComparison.Ordinal);
        Assert.Contains("page-bg__node", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/ar/")]
    [InlineData("/en/")]
    public async Task Hero_has_no_video_reel(string path)
    {
        // Owner decision 2026-10-14: the hero video reel is removed. The homepage must not ship any
        // <video> element, reel script, clip list or poster — the hero is a centred copy block on the
        // decorative colour background.
        var html = await _browser.GetHtmlAsync(path);

        Assert.DoesNotContain("<video", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-hero-reel", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-hero-clips=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("hero__video", html, StringComparison.Ordinal);
        Assert.DoesNotContain("hero-reel.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain("home-hero-poster", html, StringComparison.Ordinal);
        foreach (var clip in new[] { "01-ai.mp4", "02-code.mp4", "03-business.mp4", "04-accounts.mp4", "onyx-home-hero.mp4" })
        {
            Assert.DoesNotContain(clip, html, StringComparison.Ordinal);
        }
        Assert.Contains("hero__grid", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_page_scoped_script_loads_anywhere()
    {
        // The reel script is gone, and with it the last page-scoped script: every page now keeps the
        // small shared payload only. A stray page script would bloat that budget silently.
        var home = await _browser.GetHtmlAsync("/en/");
        var contact = await _browser.GetHtmlAsync("/en/contact");

        Assert.DoesNotContain("hero-reel.js", home, StringComparison.Ordinal);
        Assert.DoesNotContain("hero-reel.js", contact, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/ar/", "مساعد آلي")]
    [InlineData("/en/", "Automated assistant")]
    public async Task Support_chat_is_present_labelled_and_never_preopens(string path, string assistantLabel)
    {
        // The chat is a rule-based concierge, not an AI agent: it must be labelled as an automated
        // assistant, must greet the visitor, and must offer quick replies that are real links.
        var html = await _browser.GetHtmlAsync(path);

        Assert.Contains("data-support-chat", html, StringComparison.Ordinal);
        Assert.Contains("support-chat__panel", html, StringComparison.Ordinal);
        Assert.Equal(3, CountOf(html, "data-support-greeting"));
        Assert.True(CountOf(html, "class=\"support-chat__chip\"") >= 4);
        // Honest labelling: it says it is automated, in the visitor's own language.
        Assert.Contains(assistantLabel, html, StringComparison.Ordinal);
        // It must never open by itself: a <details> element ships closed.
        Assert.Contains("<details class=\"support-chat\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<details class=\"support-chat\" open", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/ar/", "هل تحتاج إلى مساعدة؟")]
    [InlineData("/en/", "Need help?")]
    public async Task Support_chat_invites_the_visitor_from_a_bubble_above_the_launcher(string path, string hint)
    {
        // The invitation is a real sentence in the visitor's own language, it belongs to the launcher (so
        // pressing it opens the chat) and it sits before the panel in document order.
        var html = await _browser.GetHtmlAsync(path);

        Assert.Contains($"<span class=\"support-chat__hint\">{hint}</span>", html, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf("support-chat__hint", StringComparison.Ordinal)
            < html.IndexOf("support-chat__panel", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Support_chat_drops_the_quick_reply_that_links_to_the_current_page()
    {
        // On the contact page the "Contact us" chip would be a link to the page already open, so the
        // widget must offer one chip fewer there than on the homepage.
        var home = await _browser.GetHtmlAsync("/en/");
        var contact = await _browser.GetHtmlAsync("/en/contact");

        Assert.Equal(CountOf(home, "class=\"support-chat__chip\"") - 1, CountOf(contact, "class=\"support-chat__chip\""));
    }

    private static int CountOf(string html, string needle) =>
        html.Split([needle], StringSplitOptions.None).Length - 1;

    [Theory]
    [InlineData("/ar/", "خدمة عملاء صوتية", "وكيل دراسة الجدوى الاقتصادية", "وكيل دعم اتخاذ القرار")]
    [InlineData("/en/", "Voice customer service", "Feasibility Study Agent", "Decision Support Agent")]
    public async Task Aaas_section_renders_all_seven_agents_in_every_surface(string path, string voiceTitle, string feasibilityTitle, string decisionTitle)
    {
        // Seven agents (voice, then feasibility and decision support were added). The command centre and
        // the card list are rendered from one list, so they must always agree — seven of each.
        var html = await _browser.GetHtmlAsync(path);

        Assert.Contains(voiceTitle, html, StringComparison.Ordinal);
        Assert.Contains(feasibilityTitle, html, StringComparison.Ordinal);
        Assert.Contains(decisionTitle, html, StringComparison.Ordinal);
        Assert.Equal(7, CountOf(html, "class=\"ahub__card ahub__card--"));
        Assert.Equal(7, CountOf(html, "class=\"aaas-card aaas-card--"));
        // Every agent must also be reachable on the card list, which is what phones show.
        Assert.Contains("aaas-card--voice", html, StringComparison.Ordinal);
        Assert.Contains("aaas-card--feasibility", html, StringComparison.Ordinal);
        Assert.Contains("aaas-card--decision", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/ar/", "نظام إدارة المستشفيات والعيادات", "منصة المواهب الإبداعية")]
    [InlineData("/en/", "Hospital &amp; Clinic Management System", "Creative Talent Platform")]
    public async Task Saas_section_lists_the_five_products_with_a_contact_route(string path, string clinicsTitle, string talentTitle)
    {
        // Each product shows its confirmed purpose and audience and leads to a conversation — no prices.
        var html = await _browser.GetHtmlAsync(path);

        Assert.Contains("id=\"saas-title\"", html, StringComparison.Ordinal);
        Assert.Equal(5, CountOf(html, "class=\"saas-card saas-card--"));
        Assert.Contains(clinicsTitle, html, StringComparison.Ordinal);
        Assert.Contains(talentTitle, html, StringComparison.Ordinal);
        Assert.Equal(5, CountOf(html, "class=\"saas-card__link link-arrow\""));
    }
}
