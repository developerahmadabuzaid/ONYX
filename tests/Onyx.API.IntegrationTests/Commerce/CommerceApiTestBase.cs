using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Onyx.API.IntegrationTests.Content;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.API.IntegrationTests.Leads;
using Onyx.Application.Customers;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Customers;
using Onyx.Contracts.Products;

namespace Onyx.API.IntegrationTests.Commerce;

/// <summary>
/// Helpers for the SaaS platform tests: customers sign up, confirm the emailed link and sign in through the real API; staff
/// create and publish products. Every name, product and price is fictional sample data.
/// </summary>
public abstract partial class CommerceApiTestBase(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    protected const string CustomerAuth = "/api/v1/customer-auth";
    protected const string Customer = "/api/v1/customer";
    protected const string AdminProducts = "/api/v1/admin/products";
    protected const string CustomerPassword = "sample customer passphrase";

    protected override IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>(base.Settings)
    {
        ["Site:PublicBaseUrl"] = "https://onyx.test",
        ["Turnstile:SecretKey"] = "test-secret",
        ["AiDemo:Enabled"] = "true",
        ["AiDemo:PerCustomerDailyLimit"] = "2",
    };

    private FakeTurnstile? _turnstile;

    protected FakeTurnstile Turnstile => _turnstile ??= new FakeTurnstile(Time);

    /// <summary>Customer forms fail closed without Turnstile, so the real adapter talks to <see cref="FakeTurnstile"/>.</summary>
    protected override void ConfigureTestServices(IServiceCollection services) =>
        services.AddHttpClient("ICaptchaVerifier").ConfigurePrimaryHttpMessageHandler(() => Turnstile);

    protected override string? LoginCaptchaToken() => Solved("admin-login", "admin.onyx.test");

    /// <summary>A unique solved token for <paramref name="action"/> on the public site (tokens are single use).</summary>
    protected static string Solved(string action, string host = "onyx.test") => $"ok:{action}:{host}:{Guid.NewGuid():N}";

    protected static RegisterCustomerRequest Registration(string email, string? returnPath = null) =>
        new(email, CustomerPassword, "Sample Customer", "Sample Co", "en", AcceptTerms: true, MarketingConsent: false, returnPath, Solved("customer-register"));

    protected static CustomerSignInRequest SignIn(string email, string password) => new(email, password, Solved("customer-sign-in"));

    /// <summary>The newest email of <paramref name="template"/> sent to <paramref name="email"/>.</summary>
    protected async Task<string> LatestEmailBodyAsync(string email, string template) =>
        (await QueryAsync(db => db.EmailMessages.Where(message => message.ToAddress == email && message.TemplateKey == template)))
            .OrderByDescending(message => message.CreatedAtUtc).First().TextBody;

    protected static (Guid Id, string Token) LinkParts(string body)
    {
        var id = IdPattern().Match(body);
        var token = TokenPattern().Match(body);
        Assert.True(id.Success && token.Success, "The email carries no link.");
        return (Guid.Parse(id.Groups[1].Value), Uri.UnescapeDataString(token.Groups[1].Value));
    }

    protected async Task VerifyAsync(string email)
    {
        var (id, token) = LinkParts(await LatestEmailBodyAsync(email, CustomerEmailTemplates.VerifyEmail));
        using var verified = await Client.PostAsJsonAsync($"{CustomerAuth}/verify-email", new VerifyCustomerEmailRequest(id, token), Token);
        Assert.True(verified.IsSuccessStatusCode, await verified.Content.ReadAsStringAsync(Token));
    }

    /// <summary>Registers, verifies and signs in a new customer; returns a client sending its customer session.</summary>
    protected async Task<HttpClient> NewCustomerAsync(string email)
    {
        using (var registered = await Client.PostAsJsonAsync($"{CustomerAuth}/register", Registration(email), Token))
        {
            Assert.True(registered.IsSuccessStatusCode, await registered.Content.ReadAsStringAsync(Token));
        }

        await VerifyAsync(email);
        return CustomerClient((await CustomerSignInAsync(email, CustomerPassword)).SessionToken);
    }

    protected async Task<CustomerSessionResponse> CustomerSignInAsync(string email, string password) =>
        await ReadAsync<CustomerSessionResponse>(await Client.PostAsJsonAsync($"{CustomerAuth}/sign-in", SignIn(email, password), Token));

    protected HttpClient CustomerClient(string sessionToken)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(CustomerAuthDefaults.AuthorizationScheme, sessionToken);
        return client;
    }

    protected Task<HttpClient> CatalogueAdminAsync() =>
        SignInWithAsync("catalogue", Permissions.Products.View, Permissions.Products.Edit, Permissions.Products.Publish);

    /// <summary>A product with complete copy in both languages, optionally verified and published.</summary>
    protected static async Task<AdminProductResponse> CreateProductAsync(
        HttpClient admin, string key, int? trialDays = 14, string demoKind = "None", bool publish = true)
    {
        var product = await ReadAsync<AdminProductResponse>(await admin.PostAsJsonAsync(AdminProducts, new CreateProductRequest(key), Token));
        product = await ReadAsync<AdminProductResponse>(await admin.PutAsJsonAsync($"{AdminProducts}/{product.Id}", new SaveProductRequest(
            trialDays, demoKind, null, 0,
            [
                new ProductTranslationDto("ar", "منتج تجريبي " + key, "منتج-" + key, "ملخص تجريبي.", "وصف تجريبي للمنتج."),
                new ProductTranslationDto("en", "Sample product " + key, "sample-" + key, "Sample summary.", "Sample product description."),
            ],
            product.Version), Token));
        if (!publish)
        {
            return product;
        }

        product = await ReadAsync<AdminProductResponse>(await admin.PostAsJsonAsync(
            $"{AdminProducts}/{product.Id}/verify", new VerifyProductRequest("Sample verification for tests", product.Version), Token));
        return await ReadAsync<AdminProductResponse>(await admin.PostAsJsonAsync($"{AdminProducts}/{product.Id}/publish", new ProductVersionRequest(product.Version), Token));
    }

    [GeneratedRegex(@"[?&]id=([0-9a-fA-F-]{36})")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"[?&]token=([A-Za-z0-9_%\-]+)")]
    private static partial Regex TokenPattern();
}
