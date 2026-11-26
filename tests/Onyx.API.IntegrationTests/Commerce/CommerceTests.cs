using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Customers;
using Onyx.Application.Products;
using Onyx.Application.Subscriptions;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Commerce;
using Onyx.Contracts.Common;
using Onyx.Contracts.Customers;
using Onyx.Contracts.Products;

namespace Onyx.API.IntegrationTests.Commerce;

/// <summary>
/// Products, subscriptions and trials, manual invoices and the AI demo through the real API and PostgreSQL: proof and price
/// rules, per-customer scoping, staff permissions, Worker steps (trial expiry, demo processing). All data is fictional.
/// </summary>
public sealed class CommerceTests(PostgresFixture fixture) : CommerceApiTestBase(fixture)
{
    [Fact]
    public async Task Products_publish_only_when_verified_and_visitors_see_prices_only_where_made_public()
    {
        Fixture.RequireDatabase();
        using var viewer = await SignInWithAsync("viewer", Permissions.Products.View);
        using var admin = await CatalogueAdminAsync();
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.PostAsJsonAsync(AdminProducts, new CreateProductRequest("nope"), Token)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync(AdminProducts, Token)));

        var draft = await CreateProductAsync(admin, "erp", publish: false);
        using (var refused = await admin.PostAsJsonAsync($"{AdminProducts}/{draft.Id}/publish", new ProductVersionRequest(draft.Version), Token))
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Contains("Product.Unverified", await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }

        Assert.Empty(await ReadAsync<IReadOnlyList<PublicProductListItemResponse>>(await Client.GetAsync("/api/v1/products?lang=en", Token)));
        var product = await CreateProductAsync(admin, "crm");
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(admin.PostAsJsonAsync($"{AdminProducts}/{product.Id}/plans",
            Plan("pro", 99m, null, isPublic: true, product.Version), Token)));
        product = await ReadAsync<AdminProductResponse>(await admin.PostAsJsonAsync($"{AdminProducts}/{product.Id}/plans", Plan("basic", 99m, "SAR", isPublic: true, product.Version), Token));
        product = await ReadAsync<AdminProductResponse>(await admin.PostAsJsonAsync($"{AdminProducts}/{product.Id}/plans", Plan("enterprise", 999m, "SAR", isPublic: false, product.Version), Token));

        var list = Assert.Single(await ReadAsync<IReadOnlyList<PublicProductListItemResponse>>(await Client.GetAsync("/api/v1/products?lang=ar", Token)));
        Assert.Equal("منتج-crm", list.Slug);
        Assert.True(list.HasPublicPrice);
        var detail = await ReadAsync<PublicProductResponse>(await Client.GetAsync("/api/v1/products/sample-crm?lang=en", Token));
        Assert.Equal(99m, detail.Plans.Single(plan => plan.Code == "basic").Price);
        Assert.Null(detail.Plans.Single(plan => plan.Code == "enterprise").Price);
        Assert.Null(detail.Plans.Single(plan => plan.Code == "enterprise").Currency);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.GetAsync("/api/v1/products/sample-erp?lang=en", Token)));
    }

    [Fact]
    public async Task Trials_start_once_requests_wait_for_staff_and_customers_only_see_their_own()
    {
        Fixture.RequireDatabase();
        using var admin = await CatalogueAdminAsync();
        await CreateProductAsync(admin, "clinic", trialDays: 14);
        await CreateProductAsync(admin, "retail", trialDays: null);
        using var customer = await NewCustomerAsync("buyer@example.test");
        using var stranger = await NewCustomerAsync("stranger@example.test");

        var trial = await ReadAsync<CustomerSubscriptionResponse>(await customer.PostAsJsonAsync($"{Customer}/subscriptions",
            new StartSubscriptionRequest("sample-clinic", "en", CustomerIntents.Trial, null, null), Token));
        Assert.Equal("Trial", trial.Status);
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(customer.PostAsJsonAsync($"{Customer}/subscriptions",
            new StartSubscriptionRequest("sample-clinic", "en", CustomerIntents.Subscribe, null, null), Token)));
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(customer.PostAsJsonAsync($"{Customer}/subscriptions",
            new StartSubscriptionRequest("sample-retail", "en", CustomerIntents.Trial, null, null), Token)));

        var request = await ReadAsync<CustomerSubscriptionResponse>(await customer.PostAsJsonAsync($"{Customer}/subscriptions",
            new StartSubscriptionRequest("sample-retail", "en", CustomerIntents.Subscribe, null, "Sample note"), Token));
        Assert.Equal("Requested", request.Status);
        Assert.Empty(await ReadAsync<IReadOnlyList<CustomerSubscriptionResponse>>(await stranger.GetAsync($"{Customer}/subscriptions?lang=en", Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(stranger.PostAsJsonAsync($"{Customer}/subscriptions/{request.Id}/cancel", new CancelSubscriptionRequest(request.Version), Token)));
        Assert.Equal(2, (await QueryAsync(db => db.AdminNotifications)).Count);

        // Staff decide; the customer is emailed.
        using var sales = await SignInWithAsync("sales", Permissions.Subscriptions.View, Permissions.Subscriptions.Manage);
        using var readOnly = await SignInWithAsync("reader", Permissions.Subscriptions.View);
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(readOnly.PostAsJsonAsync($"/api/v1/admin/subscriptions/{request.Id}/decision",
            new SubscriptionDecisionRequest("activate", null, null, request.Version), Token)));
        var activated = await ReadAsync<AdminSubscriptionResponse>(await sales.PostAsJsonAsync($"/api/v1/admin/subscriptions/{request.Id}/decision",
            new SubscriptionDecisionRequest("activate", null, "Sample activation", request.Version), Token));
        Assert.Equal("Active", activated.Status);
        Assert.Single(await QueryAsync(db => db.EmailMessages.Where(message => message.TemplateKey == CustomerEmailTemplates.SubscriptionUpdated)));

        // The Worker expires the trial after its end date; a second trial of the same product is refused for good.
        Time.Advance(TimeSpan.FromDays(15));
        Assert.Equal(1, await InScopeAsync(services => services.GetRequiredService<SubscriptionAdministrationService>().ExpireEndedTrialsAsync(Token)));
        Assert.Equal(0, await InScopeAsync(services => services.GetRequiredService<SubscriptionAdministrationService>().ExpireEndedTrialsAsync(Token)));
        using var later = CustomerClient((await CustomerSignInAsync("buyer@example.test", CustomerPassword)).SessionToken);
        var mine = await ReadAsync<IReadOnlyList<CustomerSubscriptionResponse>>(await later.GetAsync($"{Customer}/subscriptions?lang=en", Token));
        Assert.Equal("Expired", mine.Single(item => item.Id == trial.Id).Status);
        using var again = await later.PostAsJsonAsync($"{Customer}/subscriptions", new StartSubscriptionRequest("sample-clinic", "en", CustomerIntents.Trial, null, null), Token);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("Subscription.TrialUsed", await again.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoices_are_numbered_on_issue_and_customers_see_only_their_own_issued_invoices()
    {
        Fixture.RequireDatabase();
        using var customer = await NewCustomerAsync("billed@example.test");
        using var stranger = await NewCustomerAsync("other@example.test");
        var customerId = (await QueryAsync(db => db.CustomerAccounts.Where(account => account.Email == "billed@example.test"))).Single().Id;
        using var billing = await SignInWithAsync("billing", Permissions.Invoices.View, Permissions.Invoices.Manage);

        var draft = await ReadAsync<AdminInvoiceResponse>(await billing.PostAsJsonAsync("/api/v1/admin/invoices", new SaveInvoiceRequest(
            customerId, null, "SAR", new DateOnly(2027, 1, 31), "Sample note", [new InvoiceLineRequest("Sample setup", 1, 1000m), new InvoiceLineRequest("Sample hours", 2.5m, 200m)], null), Token));
        Assert.Equal(1500m, draft.Total);
        Assert.Null(draft.Number);
        Assert.Empty(await ReadAsync<IReadOnlyList<CustomerInvoiceResponse>>(await customer.GetAsync($"{Customer}/invoices", Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(customer.GetAsync($"{Customer}/invoices/{draft.Id}", Token)));

        var issued = await ReadAsync<AdminInvoiceResponse>(await billing.PostAsJsonAsync($"/api/v1/admin/invoices/{draft.Id}/issue", new InvoiceCommandRequest(draft.Version), Token));
        Assert.Matches(@"^INV-\d{4}-\d{5}$", issued.Number);
        Assert.Equal("Sample Co", issued.BillToName);
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(billing.PutAsJsonAsync($"/api/v1/admin/invoices/{draft.Id}", new SaveInvoiceRequest(
            customerId, null, "SAR", new DateOnly(2027, 1, 31), null, [new InvoiceLineRequest("Changed", 1, 1m)], issued.Version), Token)));

        var mine = Assert.Single(await ReadAsync<IReadOnlyList<CustomerInvoiceResponse>>(await customer.GetAsync($"{Customer}/invoices", Token)));
        Assert.Equal(issued.Number, mine.Number);
        Assert.Equal(2, mine.Lines.Count);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(stranger.GetAsync($"{Customer}/invoices/{draft.Id}", Token)));

        var paid = await ReadAsync<AdminInvoiceResponse>(await billing.PostAsJsonAsync($"/api/v1/admin/invoices/{draft.Id}/paid", new InvoiceCommandRequest(issued.Version), Token));
        Assert.Equal("Paid", paid.Status);
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(billing.PostAsJsonAsync($"/api/v1/admin/invoices/{draft.Id}/void", new InvoiceCommandRequest(paid.Version), Token)));
    }

    [Fact]
    public async Task Ai_demo_runs_are_limited_queued_for_the_worker_and_fail_visibly_without_a_provider()
    {
        Fixture.RequireDatabase();
        using var admin = await CatalogueAdminAsync();
        await CreateProductAsync(admin, "assistant", demoKind: "AiAssistant");
        await CreateProductAsync(admin, "plain");
        using var customer = await NewCustomerAsync("demo@example.test");

        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(customer.PostAsJsonAsync($"{Customer}/ai-demo/runs", new StartAiDemoRequest("sample-plain", "en", "Hello?", Solved("ai-demo")), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(customer.PostAsJsonAsync($"{Customer}/ai-demo/runs", new StartAiDemoRequest("sample-assistant", "en", new string('x', 1001), null), Token)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.PostAsJsonAsync($"{Customer}/ai-demo/runs", new StartAiDemoRequest("sample-assistant", "en", "Hello?", Solved("ai-demo")), Token)));

        var run = await ReadAsync<AiDemoRunResponse>(await customer.PostAsJsonAsync($"{Customer}/ai-demo/runs", new StartAiDemoRequest("sample-assistant", "en", "What does it do?", Solved("ai-demo")), Token));
        Assert.Equal("Queued", run.Status);
        await ReadAsync<AiDemoRunResponse>(await customer.PostAsJsonAsync($"{Customer}/ai-demo/runs", new StartAiDemoRequest("sample-assistant", "en", "Second sample?", Solved("ai-demo")), Token));
        using (var limited = await customer.PostAsJsonAsync($"{Customer}/ai-demo/runs", new StartAiDemoRequest("sample-assistant", "en", "Third sample?", Solved("ai-demo")), Token))
        {
            Assert.Equal(HttpStatusCode.Conflict, limited.StatusCode);
            Assert.Contains("AiDemo.DailyLimit", await limited.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }

        Assert.Equal(2, await InScopeAsync(services => services.GetRequiredService<AiDemoService>().ProcessQueuedAsync(null, Token)));
        var processed = await ReadAsync<AiDemoRunResponse>(await customer.GetAsync($"{Customer}/ai-demo/runs/{run.Id}?lang=en", Token));
        Assert.Equal("Failed", processed.Status);
        Assert.Equal("AiDemo.NotConnected", processed.FailureCode);

        var overview = await ReadAsync<CustomerOverviewResponse>(await customer.GetAsync($"{Customer}/overview?lang=en", Token));
        Assert.Equal(2, overview.Usage.AiDemoRunsToday);
        Assert.Equal(2, overview.Usage.AiDemoDailyLimit);
    }

    private static SavePlanRequest Plan(string code, decimal? price, string? currency, bool isPublic, uint version) =>
        new(code, price, currency, "Monthly", isPublic, true, 0,
            [new PlanTranslationDto("ar", "باقة " + code, null), new PlanTranslationDto("en", "Plan " + code, null)], version);
}
