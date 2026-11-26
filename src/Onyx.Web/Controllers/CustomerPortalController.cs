using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Onyx.Contracts.Customers;
using Onyx.Web.ApiClients;
using Onyx.Web.Configuration;
using Onyx.Web.Customers;
using Onyx.Web.Localization;
using Onyx.Web.Resources;
using Onyx.Web.Security;

namespace Onyx.Web.Controllers;

/// <summary>
/// The customer dashboard: overview, profile, subscriptions, starting a trial or subscription, invoices and the AI demo.
/// Requires the customer sign-in (never the staff one); the API scopes every call to the signed-in customer.
/// </summary>
[Authorize(AuthenticationSchemes = CustomerWebAuth.Scheme)]
[SensitivePage]
public sealed class CustomerPortalController(
    ICustomerApiClient customers,
    IProductsApiClient products,
    LocalizedRouteTable routes,
    LanguageCatalog languages,
    SeoContext seo,
    IOptions<TurnstileOptions> turnstile,
    IStringLocalizer<SharedResource> localizer) : CustomerPageController(routes, languages, seo, turnstile, localizer)
{
    [HttpGet]
    public async Task<IActionResult> Account(string lang, string? slug, CancellationToken cancellationToken)
    {
        if (slug is not null)
        {
            return NotFound();
        }

        Describe(CustomerPages.Account, lang, "Customer.Account.Title");
        var overview = await customers.OverviewAsync(lang, cancellationToken);
        if (!overview.IsSuccess)
        {
            throw new ApiUnavailableException();
        }

        return View(overview.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Profile(string lang, string? slug, CancellationToken cancellationToken)
    {
        if (slug is not null)
        {
            return NotFound();
        }

        Describe(CustomerPages.Profile, lang, "Customer.Profile.Title");
        return View(new ProfilePage(await LoadProfileAsync(cancellationToken), null));
    }

    [HttpPost]
    public async Task<IActionResult> Profile(string lang, string? slug, ProfileForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (slug is not null)
        {
            return NotFound();
        }

        Describe(CustomerPages.Profile, lang, "Customer.Profile.Title");
        switch (form.Command)
        {
            case "password":
                Require(!string.IsNullOrEmpty(form.NewPassword), nameof(form.NewPassword), "Customer.Error.PasswordRequired");
                Require(form.NewPassword == form.ConfirmPassword, nameof(form.ConfirmPassword), "Customer.Error.PasswordMismatch");
                if (ModelState.IsValid)
                {
                    var changed = await customers.ChangePasswordAsync(new CustomerChangePasswordRequest(form.CurrentPassword ?? string.Empty, form.NewPassword!), cancellationToken);
                    if (changed.IsSuccess)
                    {
                        // Every session ended, including this one: sign in again with the new password.
                        await HttpContext.SignOutAsync(CustomerWebAuth.Scheme);
                        TempData[NoticeKey] = "Customer.Profile.PasswordChanged";
                        return LocalRedirect(PathOf(CustomerPages.SignIn, lang));
                    }

                    AddApiError(changed);
                }

                return View(new ProfilePage(await LoadProfileAsync(cancellationToken), "password"));

            case "delete":
                var deleted = await customers.DeleteAccountAsync(new DeleteCustomerAccountRequest(form.DeletePassword ?? string.Empty), cancellationToken);
                if (deleted.IsSuccess)
                {
                    await HttpContext.SignOutAsync(CustomerWebAuth.Scheme);
                    return LocalRedirect("/" + lang + "/");
                }

                AddApiError(deleted);
                return View(new ProfilePage(await LoadProfileAsync(cancellationToken), "delete"));

            default:
                var updated = await customers.UpdateProfileAsync(
                    new UpdateCustomerProfileRequest(form.DisplayName ?? string.Empty, form.Company, form.Language ?? lang, form.MarketingConsent, form.Version), cancellationToken);
                if (updated.IsSuccess)
                {
                    TempData[NoticeKey] = "Customer.Profile.Saved";
                    return LocalRedirect(PathOf(CustomerPages.Profile, lang));
                }

                AddApiError(updated);
                return View(new ProfilePage(form, "profile"));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Subscriptions(string lang, string? slug, CancellationToken cancellationToken)
    {
        if (slug is not null)
        {
            return NotFound();
        }

        Describe(CustomerPages.Subscriptions, lang, "Customer.Subscriptions.Title");
        var list = await customers.SubscriptionsAsync(lang, cancellationToken);
        return list.IsSuccess ? View(list.Value) : throw new ApiUnavailableException();
    }

    [HttpPost]
    public async Task<IActionResult> Subscriptions(string lang, string? slug, [FromForm] Guid id, [FromForm] uint version, CancellationToken cancellationToken)
    {
        if (slug is not null)
        {
            return NotFound();
        }

        var cancelled = await customers.CancelSubscriptionAsync(id, version, cancellationToken);
        TempData[NoticeKey] = cancelled.IsSuccess ? "Customer.Subscriptions.Cancelled" : null;
        if (!cancelled.IsSuccess)
        {
            TempData["onyx.customer.error"] = ErrorText(cancelled.Problem);
        }

        return LocalRedirect(PathOf(CustomerPages.Subscriptions, lang));
    }

    /// <summary>The page a product's "start trial" or "subscribe" button leads to, kept through sign-in and sign-up.</summary>
    [HttpGet]
    public async Task<IActionResult> Subscribe(string lang, string? slug, [FromQuery] string? product, [FromQuery] string? intent, CancellationToken cancellationToken)
    {
        if (slug is not null || string.IsNullOrWhiteSpace(product) || intent is not (CustomerIntents.Trial or CustomerIntents.Subscribe))
        {
            return NotFound();
        }

        Describe(CustomerPages.Subscribe, lang, "Customer.Subscribe.Title");
        var found = await products.GetAsync(lang, product, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.IsUnavailable ? throw new ApiUnavailableException() : NotFound();
        }

        return View(new SubscribePage(found.Value!, intent, new SubscribeForm { Product = product, Intent = intent }));
    }

    [HttpPost]
    public async Task<IActionResult> Subscribe(string lang, string? slug, SubscribeForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (slug is not null || string.IsNullOrWhiteSpace(form.Product) || form.Intent is not (CustomerIntents.Trial or CustomerIntents.Subscribe))
        {
            return NotFound();
        }

        Describe(CustomerPages.Subscribe, lang, "Customer.Subscribe.Title");
        var started = await customers.StartSubscriptionAsync(new StartSubscriptionRequest(form.Product, lang, form.Intent, form.PlanId, form.Note), cancellationToken);
        if (started.IsSuccess)
        {
            TempData[NoticeKey] = started.Value!.IsTrial ? "Customer.Subscribe.TrialStarted" : "Customer.Subscribe.Requested";
            return LocalRedirect(PathOf(CustomerPages.Subscriptions, lang));
        }

        AddApiError(started);
        var found = await products.GetAsync(lang, form.Product, cancellationToken);
        return found.IsSuccess ? View(new SubscribePage(found.Value!, form.Intent, form)) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> Invoices(string lang, string? slug, CancellationToken cancellationToken)
    {
        if (slug is null)
        {
            Describe(CustomerPages.Invoices, lang, "Customer.Invoices.Title");
            var list = await customers.InvoicesAsync(cancellationToken);
            return list.IsSuccess ? View(list.Value) : throw new ApiUnavailableException();
        }

        if (!Guid.TryParse(slug, out var id))
        {
            return NotFound();
        }

        Describe(CustomerPages.Invoices, lang, "Customer.Invoices.DetailTitle", slug);
        var invoice = await customers.InvoiceAsync(id, cancellationToken);
        return invoice.IsSuccess ? View("Invoice", invoice.Value) : invoice.IsUnavailable ? throw new ApiUnavailableException() : NotFound();
    }

    [HttpGet]
    [AllowsTurnstile]
    public async Task<IActionResult> AiDemo(string lang, string? slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        Describe(CustomerPages.AiDemo, lang, "Customer.Demo.Title", slug);
        var page = await LoadDemoAsync(lang, slug, new AiDemoForm(), cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    [HttpPost]
    [AllowsTurnstile]
    public async Task<IActionResult> AiDemo(string lang, string? slug, AiDemoForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        Describe(CustomerPages.AiDemo, lang, "Customer.Demo.Title", slug);
        Require(!string.IsNullOrWhiteSpace(form.Prompt), nameof(form.Prompt), "Customer.Error.PromptRequired");
        if (ModelState.IsValid)
        {
            var started = await customers.StartDemoAsync(new StartAiDemoRequest(slug, lang, form.Prompt!, TurnstileToken()), cancellationToken);
            if (started.IsSuccess)
            {
                TempData[NoticeKey] = "Customer.Demo.Queued";
                return LocalRedirect(PathOf(CustomerPages.AiDemo, lang, slug));
            }

            AddApiError(started);
        }

        var page = await LoadDemoAsync(lang, slug, form, cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    private async Task<AiDemoPage?> LoadDemoAsync(string lang, string slug, AiDemoForm form, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(lang, slug, cancellationToken);
        if (!product.IsSuccess || product.Value!.DemoKind != "AiAssistant")
        {
            return product.IsUnavailable ? throw new ApiUnavailableException() : null;
        }

        var runs = await customers.DemoRunsAsync(slug, lang, cancellationToken);
        var overview = await customers.OverviewAsync(lang, cancellationToken);
        return new AiDemoPage(product.Value, runs.IsSuccess ? runs.Value! : [], form, overview.IsSuccess && overview.Value!.Usage.AiDemoAvailable);
    }

    private async Task<ProfileForm> LoadProfileAsync(CancellationToken cancellationToken)
    {
        var profile = await customers.ProfileAsync(cancellationToken);
        if (!profile.IsSuccess)
        {
            throw new ApiUnavailableException();
        }

        var value = profile.Value!;
        return new ProfileForm
        {
            DisplayName = value.DisplayName, Company = value.Company, Language = value.Language, MarketingConsent = value.MarketingConsent,
            Version = value.Version, Email = value.Email,
        };
    }
}
