using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Commerce;
using Onyx.Contracts.Products;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.Commerce;
using Onyx.Web.Authentication;
using Onyx.Web.Resources;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// SaaS commerce for staff: the product catalogue (copy, plans, prices, demos, verification, publishing), customer accounts,
/// subscription decisions and manual invoices. The API authorizes every call; the menu and buttons only follow permissions.
/// </summary>
[Route("admin/commerce")]
public sealed class CommerceController(IAdminCommerceApiClient api, CurrentAdminContext currentAdmin, IStringLocalizer<SharedResource> localizer) : CmsControllerBase
{
    private static string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? "ar" : "en";

    [HttpGet("products")]
    public async Task<IActionResult> Products(CancellationToken cancellationToken)
    {
        var products = await api.ProductsAsync(Lang, cancellationToken);
        if (Failed(products) is { } failed)
        {
            return failed;
        }

        ViewData["CanEdit"] = await HasAsync(Permissions.Products.Edit, cancellationToken);
        return View(products.Value);
    }

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromForm] string? key, CancellationToken cancellationToken)
    {
        var created = await api.CreateProductAsync(new CreateProductRequest(key ?? string.Empty), cancellationToken);
        return created.IsSuccess
            ? After(created, RedirectToAction(nameof(Product), new { id = created.Value!.Id }))
            : After(created, RedirectToAction(nameof(Products)));
    }

    [HttpGet("products/{id:guid}")]
    public async Task<IActionResult> Product(Guid id, CancellationToken cancellationToken)
    {
        var product = await api.ProductAsync(id, cancellationToken);
        return Failed(product) ?? View(await PageAsync(product.Value!, cancellationToken));
    }

    [HttpPost("products/{id:guid}")]
    public async Task<IActionResult> SaveProduct(Guid id, ProductForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var saved = await api.SaveProductAsync(id, new SaveProductRequest(
            form.TrialDays, form.DemoKind ?? "None", Blank(form.DemoUrl), form.SortOrder,
            [
                new ProductTranslationDto("ar", form.NameAr, Blank(form.SlugAr), form.SummaryAr, form.DescriptionAr),
                new ProductTranslationDto("en", form.NameEn, Blank(form.SlugEn), form.SummaryEn, form.DescriptionEn),
            ],
            form.Version), cancellationToken);
        return After(saved, RedirectToAction(nameof(Product), new { id }));
    }

    [HttpPost("products/{id:guid}/plans")]
    public async Task<IActionResult> SavePlan(Guid id, PlanForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        decimal? price = null;
        if (!string.IsNullOrWhiteSpace(form.Price))
        {
            if (!decimal.TryParse(form.Price, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return ShowError(localizer["Commerce.Error.Price"].Value, RedirectToAction(nameof(Product), new { id }));
            }

            price = parsed;
        }

        var request = new SavePlanRequest(form.Code, price, Blank(form.Currency), form.BillingPeriod ?? "Monthly", form.IsPricePublic, form.IsActive, form.SortOrder,
            [new PlanTranslationDto("ar", form.NameAr, Blank(form.DescriptionAr)), new PlanTranslationDto("en", form.NameEn, Blank(form.DescriptionEn))],
            form.Version);
        var saved = form.PlanId is { } planId
            ? await api.UpdatePlanAsync(id, planId, request, cancellationToken)
            : await api.AddPlanAsync(id, request, cancellationToken);
        return After(saved, RedirectToAction(nameof(Product), new { id }));
    }

    [HttpPost("products/{id:guid}/verify")]
    public async Task<IActionResult> VerifyProduct(Guid id, [FromForm] string? source, [FromForm] uint version, CancellationToken cancellationToken) =>
        After(await api.VerifyProductAsync(id, new VerifyProductRequest(source ?? string.Empty, version), cancellationToken), RedirectToAction(nameof(Product), new { id }));

    [HttpPost("products/{id:guid}/publish")]
    public async Task<IActionResult> PublishProduct(Guid id, [FromForm] bool publish, [FromForm] uint version, CancellationToken cancellationToken) =>
        After(await api.PublishProductAsync(id, publish, version, cancellationToken), RedirectToAction(nameof(Product), new { id }));

    [HttpGet("customers")]
    public async Task<IActionResult> Customers([FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        var customers = await api.CustomersAsync(search, status, Math.Max(1, page), cancellationToken);
        ViewData["Search"] = search;
        ViewData["Status"] = status;
        return Failed(customers) ?? View(customers.Value);
    }

    [HttpGet("customers/{id:guid}")]
    public async Task<IActionResult> Customer(Guid id, CancellationToken cancellationToken)
    {
        var customer = await api.CustomerAsync(id, Lang, cancellationToken);
        if (Failed(customer) is { } failed)
        {
            return failed;
        }

        ViewData["CanManage"] = await HasAsync(Permissions.Customers.Manage, cancellationToken);
        ViewData["CanInvoice"] = await HasAsync(Permissions.Invoices.Manage, cancellationToken);
        return View(customer.Value);
    }

    [HttpPost("customers/{id:guid}/status")]
    public async Task<IActionResult> CustomerStatus(Guid id, [FromForm] bool enabled, [FromForm] uint version, CancellationToken cancellationToken) =>
        After(await api.SetCustomerStatusAsync(id, new CustomerStatusRequest(enabled, version), Lang, cancellationToken), RedirectToAction(nameof(Customer), new { id }));

    [HttpGet("subscriptions")]
    public async Task<IActionResult> Subscriptions([FromQuery] string? status, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        var subscriptions = await api.SubscriptionsAsync(status ?? "Requested", Lang, Math.Max(1, page), cancellationToken);
        if (Failed(subscriptions) is { } failed)
        {
            return failed;
        }

        ViewData["Status"] = status ?? "Requested";
        ViewData["CanManage"] = await HasAsync(Permissions.Subscriptions.Manage, cancellationToken);
        return View(subscriptions.Value);
    }

    [HttpPost("subscriptions/{id:guid}")]
    public async Task<IActionResult> DecideSubscription(
        Guid id, [FromForm] string? decision, [FromForm] DateOnly? endsOn, [FromForm] string? note, [FromForm] uint version, [FromForm] string? status, CancellationToken cancellationToken) =>
        After(await api.DecideSubscriptionAsync(id, new SubscriptionDecisionRequest(decision ?? string.Empty, endsOn, Blank(note), version), Lang, cancellationToken),
            RedirectToAction(nameof(Subscriptions), new { status }));

    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices([FromQuery] string? status, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        var invoices = await api.InvoicesAsync(status, null, Math.Max(1, page), cancellationToken);
        ViewData["Status"] = status;
        return Failed(invoices) ?? View(invoices.Value);
    }

    [HttpGet("invoices/new")]
    public async Task<IActionResult> NewInvoice([FromQuery] Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await api.CustomerAsync(customerId, Lang, cancellationToken);
        if (Failed(customer) is { } failed)
        {
            return failed;
        }

        ViewData["Customer"] = customer.Value;
        return View("InvoiceEdit", new InvoiceForm { CustomerId = customerId, DueOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)), Currency = "SAR" });
    }

    [HttpPost("invoices/new")]
    public async Task<IActionResult> CreateInvoice(InvoiceForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (InvoiceLines.Parse(form.Lines) is not { } lines)
        {
            return ShowError(localizer["Commerce.Error.Lines"].Value, RedirectToAction(nameof(NewInvoice), new { customerId = form.CustomerId }));
        }

        var created = await api.CreateInvoiceAsync(new SaveInvoiceRequest(form.CustomerId, form.SubscriptionId, form.Currency ?? string.Empty, form.DueOn, Blank(form.Notes), lines, null), cancellationToken);
        return created.IsSuccess
            ? After(created, RedirectToAction(nameof(Invoice), new { id = created.Value!.Id }))
            : After(created, RedirectToAction(nameof(NewInvoice), new { customerId = form.CustomerId }));
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<IActionResult> Invoice(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await api.InvoiceAsync(id, cancellationToken);
        if (Failed(invoice) is { } failed)
        {
            return failed;
        }

        ViewData["CanManage"] = await HasAsync(Permissions.Invoices.Manage, cancellationToken);
        return View(invoice.Value);
    }

    [HttpPost("invoices/{id:guid}")]
    public async Task<IActionResult> UpdateInvoice(Guid id, InvoiceForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (InvoiceLines.Parse(form.Lines) is not { } lines)
        {
            return ShowError(localizer["Commerce.Error.Lines"].Value, RedirectToAction(nameof(Invoice), new { id }));
        }

        var saved = await api.UpdateInvoiceAsync(id, new SaveInvoiceRequest(form.CustomerId, form.SubscriptionId, form.Currency ?? string.Empty, form.DueOn, Blank(form.Notes), lines, form.Version), cancellationToken);
        return After(saved, RedirectToAction(nameof(Invoice), new { id }));
    }

    [HttpPost("invoices/{id:guid}/{command:regex(^(issue|paid|void)$)}")]
    public async Task<IActionResult> InvoiceCommand(Guid id, string command, [FromForm] uint version, CancellationToken cancellationToken) =>
        After(await api.InvoiceCommandAsync(id, command, version, cancellationToken), RedirectToAction(nameof(Invoice), new { id }));

    private async Task<bool> HasAsync(string permission, CancellationToken cancellationToken) =>
        (await currentAdmin.GetAsync(cancellationToken)).Permissions.Contains(permission, StringComparer.Ordinal);

    private async Task<ProductEditPage> PageAsync(AdminProductResponse product, CancellationToken cancellationToken)
    {
        var ar = product.Translations.FirstOrDefault(translation => translation.Language == "ar");
        var en = product.Translations.FirstOrDefault(translation => translation.Language == "en");
        var form = new ProductForm
        {
            TrialDays = product.TrialDays, DemoKind = product.DemoKind, DemoUrl = product.DemoUrl, SortOrder = product.SortOrder, Version = product.Version,
            NameAr = ar?.Name, SlugAr = ar?.Slug, SummaryAr = ar?.Summary, DescriptionAr = ar?.Description,
            NameEn = en?.Name, SlugEn = en?.Slug, SummaryEn = en?.Summary, DescriptionEn = en?.Description,
        };
        return new ProductEditPage(product, form, await HasAsync(Permissions.Products.Edit, cancellationToken), await HasAsync(Permissions.Products.Publish, cancellationToken));
    }
}
