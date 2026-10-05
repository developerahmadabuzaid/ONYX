using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Onyx.Contracts.Identity;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Models;
using Onyx.Web.Authentication;
using Onyx.Web.Localization;
using Onyx.Web.Resources;
using Onyx.Web.Security;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin sign-in and account flows. Challenge tokens and one-time link tokens are kept in the encrypted, HttpOnly TempData
/// cookie, never in page markup; links carrying tokens are immediately redirected to a clean URL.
/// </summary>
[Area("Admin")]
[Route("admin/account")]
[SensitivePage]
public sealed partial class AccountController(
    IAuthApiClient authApi,
    IAdminApiClient adminApi,
    AdminSignInService signIn,
    LanguageCatalog languages,
    IStringLocalizer<SharedResource> localizer,
    Microsoft.Extensions.Options.IOptions<Configuration.TurnstileOptions> turnstile) : Controller
{
    private const string ChallengeKey = "onyx.challenge";
    private const string EnrollmentKey = "onyx.enrollment";
    private const string ReturnKey = "onyx.return";
    private const string ResetKey = "onyx.reset";
    private const string InvitationKey = "onyx.invite";
    private const string NoticeKey = "onyx.notice";

    /// <summary>The Turnstile site key for the sign-in and password-reset pages (the API verifies the token when configured).</summary>
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        ViewData["TurnstileSiteKey"] = turnstile.Value.IsConfigured ? turnstile.Value.SiteKey : null;
        base.OnActionExecuting(context);
    }

    private string? CaptchaToken() => Request.HasFormContentType ? Request.Form[Leads.LeadFormMapper.TurnstileField].ToString() : null;

    private static string? CaptchaMessage(Onyx.Contracts.Common.ApiProblem? problem) => problem?.Code switch
    {
        "Captcha.Failed" => "Form.Error.Captcha",
        "Captcha.Unavailable" => "Form.Error.Unavailable",
        _ => null,
    };

    [HttpGet("login")]
    [AllowAnonymous]
    [AllowsTurnstile]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDashboard();
        }

        if (SafeAdminUrl(returnUrl) is { } safeReturnUrl)
        {
            TempData[ReturnKey] = safeReturnUrl;
        }
        else
        {
            TempData.Remove(ReturnKey);
        }

        return View(new LoginModel());
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [AllowsTurnstile]
    public async Task<IActionResult> Login(LoginModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        TempData.Keep(ReturnKey);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authApi.LoginAsync(new LoginRequest(model.Email, model.Password, CaptchaToken()), cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, localizer[CaptchaMessage(result.Problem) ?? (IsCredentialFailure(result.StatusCode) ? "Login.Invalid" : "Login.Unavailable")]);
            return View(new LoginModel { Email = model.Email });
        }

        switch (result.Value.Status)
        {
            case LoginStatus.TwoFactorRequired:
                TempData[ChallengeKey] = result.Value.ChallengeToken;
                return RedirectToAction(nameof(TwoFactor));
            case LoginStatus.TwoFactorEnrollmentRequired:
                TempData[EnrollmentKey] = result.Value.ChallengeToken;
                return RedirectToAction(nameof(Enroll));
            default:
                return await CompleteSignInAsync(result.Value.Tokens!, cancellationToken);
        }
    }

    [HttpGet("two-factor")]
    [AllowAnonymous]
    public IActionResult TwoFactor()
    {
        TempData.Keep(ReturnKey);
        return TempData.Peek(ChallengeKey) is string ? View(new TwoFactorModel()) : RedirectToAction(nameof(Login));
    }

    [HttpPost("two-factor")]
    [AllowAnonymous]
    public async Task<IActionResult> TwoFactor(TwoFactorModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        TempData.Keep(ReturnKey);
        if (TempData.Peek(ChallengeKey) is not string challenge)
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Code) == string.IsNullOrWhiteSpace(model.RecoveryCode))
        {
            ModelState.AddModelError(string.Empty, localizer["TwoFactor.Invalid"]);
            return View(new TwoFactorModel());
        }

        var result = await authApi.CompleteTwoFactorAsync(new TwoFactorLoginRequest(challenge, model.Code, model.RecoveryCode), cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            TempData.Remove(ChallengeKey);
            return await CompleteSignInAsync(result.Value, cancellationToken);
        }

        if (result.Problem?.Code == "Auth.InvalidChallenge")
        {
            TempData.Remove(ChallengeKey);
            TempData[NoticeKey] = "TwoFactor.Expired";
            return RedirectToAction(nameof(Login));
        }

        ModelState.AddModelError(string.Empty, localizer[result.IsUnavailable ? "Login.Unavailable" : "TwoFactor.Invalid"]);
        return View(new TwoFactorModel());
    }

    [HttpGet("two-factor/setup")]
    [AllowAnonymous]
    public async Task<IActionResult> Enroll(CancellationToken cancellationToken)
    {
        TempData.Keep(ReturnKey);
        if (TempData.Peek(EnrollmentKey) is not string token)
        {
            return RedirectToAction(nameof(Login));
        }

        var started = await authApi.StartEnrollmentAsync(new TwoFactorEnrollmentStartRequest(token), cancellationToken);
        if (started.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        if (!started.IsSuccess || started.Value is null)
        {
            return ExpiredStep(EnrollmentKey);
        }

        return EnrollView(new EnrollModel { SharedKey = started.Value.SharedKey, AuthenticatorUri = started.Value.AuthenticatorUri });
    }

    [HttpPost("two-factor/setup")]
    [AllowAnonymous]
    public async Task<IActionResult> Enroll(EnrollModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        TempData.Keep(ReturnKey);
        if (TempData.Peek(EnrollmentKey) is not string token)
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return EnrollView(model);
        }

        var completed = await authApi.CompleteEnrollmentAsync(new TwoFactorEnrollmentCompleteRequest(token, model.Code), cancellationToken);
        if (completed.IsSuccess && completed.Value is not null)
        {
            TempData.Remove(EnrollmentKey);
            if (!await signIn.SignInAsync(HttpContext, completed.Value.Tokens, cancellationToken))
            {
                return ExpiredStep(EnrollmentKey);
            }

            // Shown once, in this response only (no-store); never stored by the Web app.
            return View("RecoveryCodes", completed.Value.RecoveryCodes);
        }

        if (completed.Problem?.Code == "Auth.InvalidChallenge")
        {
            return ExpiredStep(EnrollmentKey);
        }

        ModelState.AddModelError(nameof(EnrollModel.Code), localizer[completed.IsUnavailable ? "Login.Unavailable" : "TwoFactor.Invalid"]);
        return EnrollView(model);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = await HttpContext.GetTokenAsync(BffDefaults.Scheme, BffDefaults.RefreshTokenName);
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await authApi.LogoutAsync(new LogoutRequest(refreshToken), cancellationToken);
        }

        await HttpContext.SignOutAsync(BffDefaults.Scheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("forgot-password")]
    [AllowAnonymous]
    [AllowsTurnstile]
    public IActionResult ForgotPassword() => View(new ForgotPasswordModel());

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [AllowsTurnstile]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authApi.ForgotPasswordAsync(new ForgotPasswordRequest(model.Email, CaptchaToken()), cancellationToken);
        if (result.IsUnavailable || CaptchaMessage(result.Problem) is not null)
        {
            ModelState.AddModelError(string.Empty, localizer[CaptchaMessage(result.Problem) ?? "Login.Unavailable"]);
            return View(model);
        }

        // Same answer whether or not the address belongs to an account.
        return View("Message", "Forgot.Sent");
    }

    /// <summary>Emailed link target. The token is moved into encrypted TempData and the browser is sent to a clean URL.</summary>
    [HttpGet("reset-password")]
    [AllowAnonymous]
    public IActionResult ResetPassword(Guid? userId, string? token) => TokenLinkPage(ResetKey, nameof(ResetPassword), userId, token, "Reset.Title");

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public Task<IActionResult> ResetPassword(NewPasswordModel model, CancellationToken cancellationToken) =>
        SubmitTokenLinkAsync(ResetKey, model, "Reset.Title", "Reset.Done",
            (userId, token) => authApi.ResetPasswordAsync(new ResetPasswordRequest(userId, token, model.NewPassword), cancellationToken));

    [HttpGet("accept-invitation")]
    [AllowAnonymous]
    public IActionResult AcceptInvitation(Guid? userId, string? token) => TokenLinkPage(InvitationKey, nameof(AcceptInvitation), userId, token, "Invite.Title");

    [HttpPost("accept-invitation")]
    [AllowAnonymous]
    public Task<IActionResult> AcceptInvitation(NewPasswordModel model, CancellationToken cancellationToken) =>
        SubmitTokenLinkAsync(InvitationKey, model, "Invite.Title", "Invite.Done",
            (userId, token) => authApi.AcceptInvitationAsync(new AcceptInvitationRequest(userId, token, model.NewPassword), cancellationToken));

    [HttpGet("change-password")]
    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordModel());

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!ModelState.IsValid)
        {
            return View(new ChangePasswordModel());
        }

        var result = await adminApi.ChangePasswordAsync(new ChangePasswordRequest(model.CurrentPassword, model.NewPassword), cancellationToken);
        if (result.IsSuccess)
        {
            // The API ended every session, including this one.
            await HttpContext.SignOutAsync(BffDefaults.Scheme);
            TempData[NoticeKey] = "ChangePassword.Done";
            return RedirectToAction(nameof(Login));
        }

        ModelState.AddModelError(string.Empty, localizer[result.IsUnavailable ? "Login.Unavailable" : result.Problem?.Code == "Auth.InvalidPassword" ? "ChangePassword.WrongCurrent" : "Password.Rejected"]);
        return View(new ChangePasswordModel());
    }

    [HttpGet("access-denied")]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    /// <summary>Explicit language choice on the sign-in pages (before the admin's own preference is known).</summary>
    [HttpGet("/admin/language")]
    [AllowAnonymous]
    public IActionResult Language(string to, string? returnUrl)
    {
        if (languages.IsSupported(to))
        {
            Response.Cookies.Append(LanguageCatalog.ChoiceCookieName, to, new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Path = "/",
                MaxAge = TimeSpan.FromDays(365),
            });
        }

        return LocalRedirect(SafeAdminUrl(returnUrl) ?? "/admin/account/login");
    }

    private IActionResult TokenLinkPage(string tempDataKey, string action, Guid? userId, string? token, string titleKey)
    {
        if (userId is { } id && token is not null && TokenShape().IsMatch(token))
        {
            TempData[tempDataKey] = $"{id:D}|{token}";
            return RedirectToAction(action, new { userId = (Guid?)null, token = (string?)null });
        }

        ViewData["TitleKey"] = titleKey;
        return TempData.Peek(tempDataKey) is string ? View("SetPassword", new NewPasswordModel()) : View("Message", "Link.Invalid");
    }

    private async Task<IActionResult> SubmitTokenLinkAsync(
        string tempDataKey, NewPasswordModel model, string titleKey, string doneKey, Func<Guid, string, Task<ApiResult<NoContent>>> submit)
    {
        ViewData["TitleKey"] = titleKey;
        if (TempData.Peek(tempDataKey) is not string stored || stored.Split('|') is not [var idText, var token] || !Guid.TryParse(idText, out var userId))
        {
            return View("Message", "Link.Invalid");
        }

        if (!ModelState.IsValid)
        {
            return View("SetPassword", new NewPasswordModel());
        }

        var result = await submit(userId, token);
        if (result.IsSuccess)
        {
            TempData.Remove(tempDataKey);
            return View("Message", doneKey);
        }

        if (result.Problem?.Code == "Auth.InvalidToken")
        {
            TempData.Remove(tempDataKey);
            return View("Message", "Link.Invalid");
        }

        ModelState.AddModelError(string.Empty, localizer[result.IsUnavailable ? "Login.Unavailable" : "Password.Rejected"]);
        return View("SetPassword", new NewPasswordModel());
    }

    private async Task<IActionResult> CompleteSignInAsync(TokenPairResponse tokens, CancellationToken cancellationToken)
    {
        if (!await signIn.SignInAsync(HttpContext, tokens, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, localizer["Login.Unavailable"]);
            return View(nameof(Login), new LoginModel());
        }

        var returnUrl = TempData[ReturnKey] as string;
        return returnUrl is not null ? LocalRedirect(returnUrl) : RedirectToDashboard();
    }

    // The QR is derived from the URI of the current attempt for this response only; it is never bound, stored or logged.
    private ViewResult EnrollView(EnrollModel model)
    {
        model.QrCodeDataUri = AuthenticatorQrCode.ToDataUri(model.AuthenticatorUri);
        return View(nameof(Enroll), model);
    }

    private RedirectToActionResult ExpiredStep(string tempDataKey)
    {
        TempData.Remove(tempDataKey);
        TempData[NoticeKey] = "TwoFactor.Expired";
        return RedirectToAction(nameof(Login));
    }

    private RedirectToActionResult RedirectToDashboard() => RedirectToAction("Index", "Dashboard", new { area = "Admin" });

    private string? SafeAdminUrl(string? url) =>
        url is not null && Url.IsLocalUrl(url) && url.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) ? url : null;

    private static bool IsCredentialFailure(System.Net.HttpStatusCode status) =>
        status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.BadRequest;

    [GeneratedRegex("^[A-Za-z0-9_-]{16,2048}$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenShape();
}
