using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Onyx.Web.ApiClients.Handlers;
using Onyx.Web.Areas.Admin.Navigation;
using Onyx.Web.Localization;

namespace Onyx.Web.Tests;

public sealed class LocalizationAndHandlerUnitTests
{
    private static LocalizedRoute Services() => new(
        "services",
        "Services",
        "Index",
        new Dictionary<string, string> { ["ar"] = "الخدمات", ["en"] = "services" });

    [Fact]
    public void Localized_paths_are_percent_encoded_UTF8()
    {
        var table = new LocalizedRouteTable();
        table.Register(Services());

        Assert.Equal("/ar/%D8%A7%D9%84%D8%AE%D8%AF%D9%85%D8%A7%D8%AA/%D8%AD%D9%84%D9%88%D9%84", table.PathFor("services", "ar", "حلول"));
        Assert.Equal("/en/services/ai-solutions", table.PathFor("services", "en", "ai-solutions"));
        Assert.Null(table.PathFor("services", "fr"));
        Assert.Null(table.PathFor("unknown", "en"));
    }

    [Fact]
    public void Segments_match_regardless_of_Unicode_normalization_form()
    {
        var table = new LocalizedRouteTable();
        table.Register(Services());

        Assert.True(table.TryMatch("ar", "الخدمات".Normalize(NormalizationForm.FormD), out var route));
        Assert.Equal("services", route.Key);
        Assert.False(table.TryMatch("en", "الخدمات", out _));
    }

    [Fact]
    public void A_segment_cannot_belong_to_two_pages()
    {
        var table = new LocalizedRouteTable();
        table.Register(Services());

        Assert.Throws<InvalidOperationException>(() => table.Register(
            new LocalizedRoute("other", "Other", "Index", new Dictionary<string, string> { ["en"] = "services" })));
    }

    [Fact]
    public async Task The_transformer_maps_localized_segments_and_sends_other_paths_to_cms_content()
    {
        var table = new LocalizedRouteTable();
        table.Register(Services());
        var transformer = new LocalizedRouteTransformer(table, new Onyx.Web.Content.SitePages.SitePageCatalog([]));

        var matched = await transformer.TransformAsync(new DefaultHttpContext(), new RouteValueDictionary { ["lang"] = "ar", ["path"] = "الخدمات/حلول-الذكاء-الاصطناعي" });
        var unknown = await transformer.TransformAsync(new DefaultHttpContext(), new RouteValueDictionary { ["lang"] = "en", ["path"] = "nope" });

        Assert.Equal("Services", matched["controller"]);
        Assert.Equal("حلول-الذكاء-الاصطناعي", matched["slug"]);
        Assert.Equal("Page", unknown["controller"]);
        Assert.Equal("Show", unknown["action"]);
        Assert.Equal("nope", unknown["path"]);
    }

    [Fact]
    public void Navigation_hides_items_the_admin_has_no_permission_for()
    {
        IReadOnlyList<AdminNavigationItem> items =
        [
            new("Nav.Dashboard", "Dashboard", "Index", null),
            new("Nav.Leads", "Leads", "Index", "Leads.View"),
            new("Nav.Users", "Users", "Index", "Users.Manage"),
        ];

        var visible = AdminNavigation.VisibleTo(["Leads.View"], items);

        Assert.Equal(["Nav.Dashboard", "Nav.Leads"], visible.Select(item => item.ResourceKey));
    }

    [Fact]
    public async Task API_calls_carry_the_resolved_client_address_language_and_correlation_id()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.66";
        var capture = new CapturingHandler();
        using var handler = new RequestContextHandler(new HttpContextAccessor { HttpContext = context }) { InnerHandler = capture };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.onyx.test/api/v1/x"));
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "10.0.0.1");

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("ar");
        try
        {
            using var _ = await invoker.SendAsync(request, TestContext.Current.CancellationToken);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }

        // Only the address resolved by the forwarded-headers middleware (trusted proxies only) is passed on.
        Assert.Equal("203.0.113.7", capture.Request!.Headers.GetValues("X-Forwarded-For").Single());
        Assert.Equal("ar", capture.Request.Headers.AcceptLanguage.ToString());
        Assert.Matches("^[0-9a-f]{32}$", capture.Request.Headers.GetValues("X-Correlation-ID").Single());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
