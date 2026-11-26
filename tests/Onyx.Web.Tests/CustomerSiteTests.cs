using System.Net;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Commerce;
using Onyx.Contracts.Customers;
using Onyx.Contracts.Products;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>
/// Public products and the customer area against a fake API: browsing without an account, the chosen product and intent kept
/// through sign-in, the customer session sent only as <c>OnyxCustomer</c>, and the customer and staff sign-ins never opening
/// each other's area. All data is fictional sample data.
/// </summary>
public sealed class CustomerSiteTests : IAsyncLifetime
{
    private const string Hostile = "<script>alert('x')</script>";
    private static readonly Guid ProductId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();

    private readonly OnyxWebFactory _factory = new();
    private HttpClient _browser = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
    public async Task Visitors_browse_products_and_see_prices_only_where_the_api_gives_one()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/products", _ => FakeOnyxApi.Json(new[] { ListItem() }));
        _factory.Api.On(HttpMethod.Get, "api/v1/products/sample-crm", _ => FakeOnyxApi.Json(Product()));

        var list = await _browser.GetHtmlAsync("/en/products");
        Assert.Contains("href=\"/en/products/sample-crm\"", list, StringComparison.Ordinal);
        Assert.Contains("href=\"/en/sign-in\"", list, StringComparison.Ordinal);
        Assert.DoesNotContain(Hostile, list, StringComparison.Ordinal);

        var detail = await _browser.GetHtmlAsync("/en/products/sample-crm");
        Assert.Contains("99.00 SAR", detail, StringComparison.Ordinal);
        Assert.Contains("href=\"/en/subscribe?product=sample-crm&amp;intent=trial\"", detail, StringComparison.Ordinal);
        Assert.Contains("href=\"/en/ai-demo/sample-crm\"", detail, StringComparison.Ordinal);
        Assert.Contains("Priced to your needs", detail, StringComparison.Ordinal);
        Assert.DoesNotContain(Hostile, detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Customer_pages_send_visitors_to_sign_in_and_keep_the_chosen_product_and_intent()
    {
        using var account = await _browser.GetPathAsync("/en/account");
        Assert.Equal(HttpStatusCode.Redirect, account.StatusCode);
        Assert.Equal("/en/sign-in?returnUrl=%2Fen%2Faccount", account.Location());

        using var subscribe = await _browser.GetPathAsync("/en/subscribe?product=sample-crm&intent=trial");
        Assert.Equal(HttpStatusCode.Redirect, subscribe.StatusCode);
        Assert.Equal("/en/sign-in?returnUrl=%2Fen%2Fsubscribe%3Fproduct%3Dsample-crm%26intent%3Dtrial", subscribe.Location());

        var signIn = await _browser.GetHtmlAsync(subscribe.Location()!);
        Assert.Contains("value=\"/en/subscribe?product=sample-crm&amp;intent=trial\"", signIn, StringComparison.Ordinal);
        Assert.Contains("href=\"/en/create-account?returnUrl=%2Fen%2Fsubscribe%3Fproduct%3Dsample-crm%26intent%3Dtrial\"", signIn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Signing_in_keeps_the_session_in_the_customer_cookie_and_sends_it_only_as_OnyxCustomer()
    {
        var token = SignInSucceeds();
        _factory.Api.On(HttpMethod.Get, "api/v1/customer/overview", _ => FakeOnyxApi.Json(Overview()));

        using var signedIn = await SignInAsync("/en/subscribe?product=sample-crm&intent=trial");
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal("/en/subscribe?product=sample-crm&intent=trial", signedIn.Location());
        var cookie = Assert.Single(signedIn.SetCookies("__Host-onyx.customer"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, cookie, StringComparison.Ordinal);

        var html = await _browser.GetHtmlAsync("/en/account");
        Assert.Contains("Welcome, Sample Customer", html, StringComparison.Ordinal);
        Assert.DoesNotContain(token, html, StringComparison.Ordinal);
        var sent = _factory.Api.RequestsTo("api/v1/customer/overview").Last();
        Assert.Equal(CustomerAuthDefaults.AuthorizationScheme, sent.AuthorizationScheme);
        Assert.Equal(token, sent.BearerToken);
    }

    [Theory]
    [InlineData("/en/sign-in")]
    [InlineData("/en/create-account")]
    [InlineData("/en/forgot-password")]
    [InlineData("/ar/%D8%AA%D8%B3%D8%AC%D9%8A%D9%84-%D8%A7%D9%84%D8%AF%D8%AE%D9%88%D9%84")]
    public async Task Customer_forms_carry_their_own_antiforgery_token(string path)
    {
        var html = await _browser.GetHtmlAsync(path);
        var form = System.Text.RegularExpressions.Regex.Match(html, "<form method=\"post\"[^>]*>(.*?)</form>", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(form.Success);
        Assert.Contains("name=\"__RequestVerificationToken\"", form.Groups[1].Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sign_in_never_redirects_off_site()
    {
        SignInSucceeds();
        using var signedIn = await SignInAsync("https://evil.example/en/");
        Assert.Equal("/en/account", signedIn.Location());
    }

    [Fact]
    public async Task A_customer_sign_in_never_opens_the_admin_area()
    {
        SignInSucceeds();
        using var signedIn = await SignInAsync(null);
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        using var admin = await _browser.GetPathAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.Contains("/admin/account/login", admin.Location(), StringComparison.Ordinal);
        Assert.Empty(_factory.Api.RequestsTo("api/v1/admin/notifications/unread-count"));
    }

    [Fact]
    public async Task A_staff_sign_in_never_opens_the_customer_area()
    {
        AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("en", Permissions.Customers.View));
        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        using var account = await _browser.GetPathAsync("/en/account");
        Assert.Equal(HttpStatusCode.Redirect, account.StatusCode);
        Assert.StartsWith("/en/sign-in", account.Location(), StringComparison.Ordinal);
        Assert.Empty(_factory.Api.RequestsTo("api/v1/customer/overview"));
    }

    [Fact]
    public async Task An_ended_api_session_signs_the_customer_out_and_returns_them_to_sign_in()
    {
        SignInSucceeds();
        using (await SignInAsync(null))
        {
        }

        _factory.Api.On(HttpMethod.Get, "api/v1/customer/overview", _ => FakeOnyxApi.Problem(HttpStatusCode.Unauthorized, "Customer.NotSignedIn"));
        using var account = await _browser.GetPathAsync("/en/account");
        Assert.Equal(HttpStatusCode.Redirect, account.StatusCode);
        Assert.Equal("/en/sign-in?returnUrl=%2Fen%2Faccount", account.Location());
        using var again = await _browser.GetPathAsync("/en/account");
        Assert.Equal("/en/sign-in?returnUrl=%2Fen%2Faccount", again.Location());
    }

    [Fact]
    public async Task Admin_commerce_menu_follows_permissions()
    {
        AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("en", Permissions.Products.View));
        using (await AdminApiStubs.LogInAsync(_browser))
        {
        }

        _factory.Api.On(HttpMethod.Get, "api/v1/admin/products", _ => FakeOnyxApi.Json(Array.Empty<AdminProductListItemResponse>()));
        var html = await _browser.GetHtmlAsync("/admin/commerce/products");
        Assert.Contains("href=\"/admin/commerce/products\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin/commerce/invoices\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"key\"", html, StringComparison.Ordinal);
    }

    private string SignInSucceeds()
    {
        var token = AdminApiStubs.NewRefreshToken();
        _factory.Api.On(HttpMethod.Post, "api/v1/customer-auth/sign-in", _ => FakeOnyxApi.Json(
            new CustomerSessionResponse(token, _factory.Time.GetUtcNow().AddDays(30), CustomerId, "Sample Customer", "en")));
        return token;
    }

    private async Task<HttpResponseMessage> SignInAsync(string? returnUrl)
    {
        var page = await _browser.GetHtmlAsync("/en/sign-in");
        var fields = new Dictionary<string, string> { ["Email"] = "sample@example.test", ["Password"] = "sample customer passphrase" };
        if (returnUrl is not null)
        {
            fields["ReturnUrl"] = returnUrl;
        }

        return await _browser.PostFormAsync("/en/sign-in", fields, BrowserExtensions.AntiforgeryToken(page));
    }

    private static PublicProductListItemResponse ListItem() =>
        new(ProductId, $"Sample {Hostile}", "sample-crm", "Sample summary", 14, "AiAssistant", true, new Dictionary<string, string> { ["ar"] = "منتج", ["en"] = "sample-crm" });

    private static PublicProductResponse Product() =>
        new(ProductId, $"Sample {Hostile}", "sample-crm", "Sample summary", "First paragraph.\n\nSecond paragraph.", 14, "AiAssistant", null,
            [
                new PublicPlanResponse(Guid.CreateVersion7(), "basic", "Basic", null, 99m, "SAR", "Monthly"),
                new PublicPlanResponse(Guid.CreateVersion7(), "enterprise", "Enterprise", null, null, null, "Monthly"),
            ],
            new Dictionary<string, string> { ["ar"] = "منتج", ["en"] = "sample-crm" });

    private static CustomerOverviewResponse Overview() =>
        new(new CustomerProfileResponse(CustomerId, "sample@example.test", "Sample Customer", null, "en", false, "1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 3),
            [], [], new CustomerUsageResponse(false, 0, 10, 0));
}
