using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
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
/// Customer sign-in, sign-up, email verification, password recovery and sign-out (public, never indexed). The API decides;
/// this only renders forms and keeps the session token inside the encrypted customer cookie. Separate from the staff sign-in.
/// </summary>
[SensitivePage]
[AllowsTurnstile]
public sealed class CustomerAccountController(
    ICustomerAuthApiClient auth,
    ICustomerApiClient customers,
    LocalizedRouteTable routes,
    LanguageCatalog languages,
    SeoContext seo,
    IOptions<TurnstileOptions> turnstile,
    IStringLocalizer<SharedResource> localizer) : CustomerPageController(routes, languages, seo, turnstile, localizer)
{
    public const string SentSlug = "sent";
    public const string ResendSlug = "resend";

    [HttpGet]
    public async Task<IActionResult> SignIn(string lang, string? slug, [FromQuery] string? returnUrl)
    {
        if (slug is not null)
        {
            return NotFound();
        }

        if ((await HttpContext.AuthenticateAsync(CustomerWebAuth.Scheme)).Succeeded)
        {
            return LocalRedirect(CustomerPages.SafeReturnUrl(returnUrl) ?? PathOf(CustomerPages.Account, lang));
        }

        Describe(CustomerPages.SignIn, lang, "Customer.SignIn.Title");
        return View(new SignInForm { ReturnUrl = CustomerPages.SafeReturnUrl(returnUrl) });
    }

    [HttpPost]
    public async Task<IActionResult> SignIn(string lang, string? slug, SignInForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        Describe(CustomerPages.SignIn, lang, "Customer.SignIn.Title");
        form.ReturnUrl = CustomerPages.SafeReturnUrl(form.ReturnUrl);
        Require(!string.IsNullOrWhiteSpace(form.Email), nameof(form.Email), "Customer.Error.EmailRequired");
        Require(!string.IsNullOrEmpty(form.Password), nameof(form.Password), "Customer.Error.PasswordRequired");
        if (slug is not null || !ModelState.IsValid)
        {
            return slug is not null ? NotFound() : View(form);
        }

        var result = await auth.SignInAsync(new CustomerSignInRequest(form.Email!, form.Password!, TurnstileToken()), cancellationToken);
        form.Password = null;
        if (!result.IsSuccess)
        {
            AddApiError(result);
            ViewData["NotVerified"] = result.Problem?.Code == "Customer.EmailNotVerified";
            return View(form);
        }

        var session = result.Value!;
        var identity = new ClaimsIdentity(
            [
                new Claim(CustomerWebAuth.CustomerIdClaim, session.CustomerId.ToString()),
                new Claim(CustomerWebAuth.NameClaim, session.DisplayName),
            ],
            CustomerWebAuth.Scheme);
        var properties = new AuthenticationProperties { IsPersistent = true, ExpiresUtc = session.AbsoluteExpiresAtUtc, AllowRefresh = false };
        properties.StoreTokens([new AuthenticationToken { Name = CustomerWebAuth.SessionTokenName, Value = session.SessionToken }]);
        await HttpContext.SignInAsync(CustomerWebAuth.Scheme, new ClaimsPrincipal(identity), properties);

        return LocalRedirect(form.ReturnUrl ?? PathOf(CustomerPages.Account, session.Language == "en" ? "en" : lang));
    }

    [HttpGet]
    public IActionResult Register(string lang, string? slug, [FromQuery] string? returnUrl)
    {
        if (slug == SentSlug)
        {
            Describe(CustomerPages.Register, lang, "Customer.Register.SentTitle", SentSlug);
            return View("RegisterSent");
        }

        if (slug is not null)
        {
            return NotFound();
        }

        Describe(CustomerPages.Register, lang, "Customer.Register.Title");
        return View(new RegisterForm { ReturnUrl = CustomerPages.SafeReturnUrl(returnUrl) });
    }

    [HttpPost]
    public async Task<IActionResult> Register(string lang, string? slug, RegisterForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (slug is not null)
        {
            return NotFound();
        }

        Describe(CustomerPages.Register, lang, "Customer.Register.Title");
        form.ReturnUrl = CustomerPages.SafeReturnUrl(form.ReturnUrl);
        Require(!string.IsNullOrWhiteSpace(form.DisplayName), nameof(form.DisplayName), "Customer.Error.NameRequired");
        Require(!string.IsNullOrWhiteSpace(form.Email), nameof(form.Email), "Customer.Error.EmailRequired");
        Require(!string.IsNullOrEmpty(form.Password), nameof(form.Password), "Customer.Error.PasswordRequired");
        Require(form.Password == form.ConfirmPassword, nameof(form.ConfirmPassword), "Customer.Error.PasswordMismatch");
        Require(form.AcceptTerms, nameof(form.AcceptTerms), "Customer.Error.Customer.TermsRequired");
        if (!ModelState.IsValid)
        {
            form.Password = form.ConfirmPassword = null;
            return View(form);
        }

        var result = await auth.RegisterAsync(
            new RegisterCustomerRequest(form.Email!, form.Password!, form.DisplayName!, form.Company, lang, form.AcceptTerms, form.MarketingConsent, form.ReturnUrl, TurnstileToken()),
            cancellationToken);
        form.Password = form.ConfirmPassword = null;
        if (!result.IsSuccess)
        {
            AddApiError(result);
            return View(form);
        }

        return LocalRedirect(PathOf(CustomerPages.Register, lang, SentSlug));
    }

    /// <summary>Shows a confirm button; the token is consumed only by the POST (mail scanners open links with GET).</summary>
    [HttpGet]
    public IActionResult VerifyEmail(string lang, string? slug, [FromQuery] Guid id, [FromQuery] string? token, [FromQuery] string? returnUrl)
    {
        if (slug == ResendSlug)
        {
            Describe(CustomerPages.VerifyEmail, lang, "Customer.Resend.Title", ResendSlug);
            return View("Resend", new EmailForm { ReturnUrl = CustomerPages.SafeReturnUrl(returnUrl) });
        }

        if (slug is not null)
        {
            return NotFound();
        }

        Describe(CustomerPages.VerifyEmail, lang, "Customer.Verify.Title");
        return View(new VerifyEmailForm { Id = id, Token = token, ReturnUrl = CustomerPages.SafeReturnUrl(returnUrl) });
    }

    [HttpPost]
    public async Task<IActionResult> VerifyEmail(string lang, string? slug, VerifyEmailForm form, EmailForm resend, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(resend);
        if (slug == ResendSlug)
        {
            Describe(CustomerPages.VerifyEmail, lang, "Customer.Resend.Title", ResendSlug);
            Require(!string.IsNullOrWhiteSpace(resend.Email), nameof(resend.Email), "Customer.Error.EmailRequired");
            if (!ModelState.IsValid)
            {
                return View("Resend", resend);
            }

            var sent = await auth.ResendVerificationAsync(
                new ResendCustomerVerificationRequest(resend.Email!, lang, CustomerPages.SafeReturnUrl(resend.ReturnUrl), TurnstileToken()), cancellationToken);
            if (!sent.IsSuccess)
            {
                AddApiError(sent);
                return View("Resend", resend);
            }

            return View("EmailSent");
        }

        if (slug is not null)
        {
            return NotFound();
        }

        Describe(CustomerPages.VerifyEmail, lang, "Customer.Verify.Title");
        form.ReturnUrl = CustomerPages.SafeReturnUrl(form.ReturnUrl);
        var result = await auth.VerifyEmailAsync(new VerifyCustomerEmailRequest(form.Id, form.Token ?? string.Empty), cancellationToken);
        if (!result.IsSuccess)
        {
            AddApiError(result);
            return View(form);
        }

        ViewData["Verified"] = true;
        return View(form);
    }

    [HttpGet]
    public IActionResult ForgotPassword(string lang, string? slug)
    {
        if (slug is not null)
        {
            return NotFound();
        }

        Describe(CustomerPages.ForgotPassword, lang, "Customer.Forgot.Title");
        return View(new EmailForm());
    }

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(string lang, string? slug, EmailForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (slug is not null)
        {
            return NotFound();
        }

        Describe(CustomerPages.ForgotPassword, lang, "Customer.Forgot.Title");
        Require(!string.IsNullOrWhiteSpace(form.Email), nameof(form.Email), "Customer.Error.EmailRequired");
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await auth.ForgotPasswordAsync(new CustomerForgotPasswordRequest(form.Email!, lang, TurnstileToken()), cancellationToken);
        if (!result.IsSuccess)
        {
            AddApiError(result);
            return View(form);
        }

        return View("EmailSent");
    }

    [HttpGet]
    public IActionResult ResetPassword(string lang, string? slug, [FromQuery] Guid id, [FromQuery] string? token)
    {
        if (slug is not null)
        {
            return NotFound();
        }

        Describe(CustomerPages.ResetPassword, lang, "Customer.Reset.Title");
        return View(new ResetPasswordForm { Id = id, Token = token });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(string lang, string? slug, ResetPasswordForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (slug is not null)
        {
            return NotFound();
        }

        Describe(CustomerPages.ResetPassword, lang, "Customer.Reset.Title");
        Require(!string.IsNullOrEmpty(form.Password), nameof(form.Password), "Customer.Error.PasswordRequired");
        Require(form.Password == form.ConfirmPassword, nameof(form.ConfirmPassword), "Customer.Error.PasswordMismatch");
        if (ModelState.IsValid)
        {
            var result = await auth.ResetPasswordAsync(new CustomerResetPasswordRequest(form.Id, form.Token ?? string.Empty, form.Password!), cancellationToken);
            if (result.IsSuccess)
            {
                TempData[NoticeKey] = "Customer.Reset.Done";
                return LocalRedirect(PathOf(CustomerPages.SignIn, lang));
            }

            AddApiError(result);
        }

        form.Password = form.ConfirmPassword = null;
        return View(form);
    }

    [HttpPost]
    public async Task<IActionResult> SignOut(string lang, string? slug, CancellationToken cancellationToken)
    {
        if ((await HttpContext.AuthenticateAsync(CustomerWebAuth.Scheme)).Succeeded)
        {
            try
            {
                await customers.SignOutAsync(cancellationToken);
            }
            catch (CustomerSessionEndedException)
            {
                // Already ended on the API side; clearing the cookie below is all that is left.
            }

            await HttpContext.SignOutAsync(CustomerWebAuth.Scheme);
        }

        return LocalRedirect("/" + (lang == "en" ? "en" : "ar") + "/");
    }
}
