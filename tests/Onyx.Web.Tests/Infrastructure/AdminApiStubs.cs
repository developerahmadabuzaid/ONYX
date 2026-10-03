using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Onyx.Contracts.Common;
using Onyx.Contracts.Identity;
using Onyx.Contracts.Notifications;

namespace Onyx.Web.Tests.Infrastructure;

/// <summary>Typical API answers for an admin session. Token values are random and realistically sized.</summary>
public static class AdminApiStubs
{
    public const string Email = "sample.admin@example.test";
    public const string Password = "sample passphrase for tests";
    public const string DisplayName = "Sample Admin";

    public static string NewAccessToken() => "eyJ" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(450));

    public static string NewRefreshToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static TokenPairResponse Tokens(TimeProvider time, TimeSpan? accessLifetime = null)
    {
        var now = time.GetUtcNow();
        return new TokenPairResponse(NewAccessToken(), now + (accessLifetime ?? TimeSpan.FromMinutes(10)), NewRefreshToken(), now.AddHours(1), now.AddHours(12));
    }

    public static CurrentUserResponse User(string language = "en", params string[] permissions) =>
        new(Guid.CreateVersion7(), Email, DisplayName, language, false, permissions);

    /// <summary>Login succeeds without 2FA; /me and the shell endpoints answer only for the current access token.</summary>
    public static TokenPairResponse SignInSucceeds(OnyxWebFactory factory, CurrentUserResponse? user = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var tokens = Tokens(factory.Time);
        factory.Api.On(HttpMethod.Post, "api/v1/auth/login", _ => FakeOnyxApi.Json(new LoginResponse(LoginStatus.Succeeded, tokens, null, null)));
        AuthenticatedEndpoints(factory, () => tokens.AccessToken, user);
        return tokens;
    }

    public static void AuthenticatedEndpoints(OnyxWebFactory factory, Func<string> currentAccessToken, CurrentUserResponse? user = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        user ??= User();
        HttpResponseMessage Guarded(RecordedApiRequest request, Func<HttpResponseMessage> respond) =>
            request.BearerToken == currentAccessToken() ? respond() : FakeOnyxApi.Problem(HttpStatusCode.Unauthorized, "Auth.Unauthorized");

        factory.Api.On(HttpMethod.Get, "api/v1/auth/me", request => Guarded(request, () => FakeOnyxApi.Json(user)));
        factory.Api.On(HttpMethod.Get, "api/v1/admin/notifications/unread-count", request => Guarded(request, () => FakeOnyxApi.Json(2)));
        factory.Api.On(HttpMethod.Get, "api/v1/admin/notifications", request => Guarded(request, () => FakeOnyxApi.Json(
            new PagedResult<AdminNotificationResponse>(
                [new AdminNotificationResponse(Guid.CreateVersion7(), "JobFailed", "Warning", new Dictionary<string, string>(), null, null, factory.Time.GetUtcNow(), false)],
                1, 20, 1))));
        factory.Api.On(HttpMethod.Post, "api/v1/auth/logout", _ => FakeOnyxApi.NoContent());
    }

    public static async Task<HttpResponseMessage> LogInAsync(HttpClient browser, string? returnUrl = null)
    {
        ArgumentNullException.ThrowIfNull(browser);
        var page = returnUrl is null ? "/admin/account/login" : $"/admin/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        return await browser.SubmitFormAsync(page, "/admin/account/login", new Dictionary<string, string>
        {
            ["Email"] = Email,
            ["Password"] = Password,
        });
    }
}
