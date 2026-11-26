using Microsoft.AspNetCore.WebUtilities;
using Onyx.Contracts.Customers;
using Onyx.Contracts.Products;
using Onyx.Web.Localization;

namespace Onyx.Web.Customers;

/// <summary>The customer and product pages: their localized routes, Turnstile actions and return-path rules.</summary>
public static class CustomerPages
{
    public const string SignIn = "customer.sign-in";
    public const string Register = "customer.register";
    public const string VerifyEmail = "customer.verify-email";
    public const string ForgotPassword = "customer.forgot-password";
    public const string ResetPassword = "customer.reset-password";
    public const string SignOut = "customer.sign-out";
    public const string Account = "customer.account";
    public const string Profile = "customer.profile";
    public const string Subscriptions = "customer.subscriptions";
    public const string Subscribe = "customer.subscribe";
    public const string Invoices = "customer.invoices";
    public const string AiDemo = "customer.ai-demo";
    public const string Products = "products";

    /// <summary>Turnstile actions; Onyx.API checks each token was solved for the same action (CustomerAuthService, AiDemoService).</summary>
    public const string RegisterCaptchaAction = "customer-register";
    public const string SignInCaptchaAction = "customer-sign-in";
    public const string RecoveryCaptchaAction = "customer-recovery";
    public const string AiDemoCaptchaAction = "ai-demo";

    public const string ReturnUrlParameter = "returnUrl";

    public static IEnumerable<LocalizedRoute> Routes =>
    [
        new(SignIn, "CustomerAccount", "SignIn", CustomerRouteSegments.SignIn),
        new(Register, "CustomerAccount", "Register", CustomerRouteSegments.Register),
        new(VerifyEmail, "CustomerAccount", "VerifyEmail", CustomerRouteSegments.VerifyEmail),
        new(ForgotPassword, "CustomerAccount", "ForgotPassword", CustomerRouteSegments.ForgotPassword),
        new(ResetPassword, "CustomerAccount", "ResetPassword", CustomerRouteSegments.ResetPassword),
        new(SignOut, "CustomerAccount", "SignOut", CustomerRouteSegments.SignOut),
        new(Account, "CustomerPortal", "Account", CustomerRouteSegments.Account),
        new(Profile, "CustomerPortal", "Profile", CustomerRouteSegments.Profile),
        new(Subscriptions, "CustomerPortal", "Subscriptions", CustomerRouteSegments.Subscriptions),
        new(Subscribe, "CustomerPortal", "Subscribe", CustomerRouteSegments.Subscribe),
        new(Invoices, "CustomerPortal", "Invoices", CustomerRouteSegments.Invoices),
        new(AiDemo, "CustomerPortal", "AiDemo", CustomerRouteSegments.AiDemo),
        new(Products, "Products", "Index", ProductRouteSegments.Products),
    ];

    public static string LanguageOf(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.StartsWith("/en/", StringComparison.Ordinal) || value == "/en" ? "en" : "ar";
    }

    public static string CurrentPath(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.PathBase + request.Path + request.QueryString;
    }

    /// <summary>Only local paths of the public site; anything else (absolute, protocol-relative, backslashes) is dropped.</summary>
    public static string? SafeReturnUrl(string? value)
    {
        var path = value?.Trim();
        if (string.IsNullOrEmpty(path) || path.Length > 500 || path.Any(char.IsControl) || path.Contains('\\', StringComparison.Ordinal)
            || path.StartsWith("//", StringComparison.Ordinal) || path.Contains("://", StringComparison.Ordinal))
        {
            return null;
        }

        return path.StartsWith("/ar/", StringComparison.Ordinal) || path.StartsWith("/en/", StringComparison.Ordinal) ? path : null;
    }

    public static string SignInUrl(LocalizedRouteTable routes, string language, string? returnUrl) =>
        WithReturn(routes.PathFor(SignIn, language) ?? "/" + language + "/", returnUrl);

    public static string WithReturn(string path, string? returnUrl) =>
        SafeReturnUrl(returnUrl) is { } safe ? QueryHelpers.AddQueryString(path, ReturnUrlParameter, safe) : path;
}
