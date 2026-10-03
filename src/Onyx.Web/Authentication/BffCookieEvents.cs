using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Onyx.Contracts.Identity;
using Onyx.Web.ApiClients;

namespace Onyx.Web.Authentication;

/// <summary>
/// Keeps the access token fresh: shortly before it expires the BFF refreshes it server-to-server and re-issues the cookie.
/// If the API refuses (revoked, idle or absolute limit reached), the cookie is rejected and the admin must sign in again.
/// </summary>
public sealed partial class BffCookieEvents(
    IAuthApiClient authApi,
    SessionRefreshCoordinator coordinator,
    TimeProvider timeProvider,
    ILogger<BffCookieEvents> logger) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var properties = context.Properties;
        var refreshToken = properties.GetTokenValue(BffDefaults.RefreshTokenName);
        var expiresText = properties.GetTokenValue(BffDefaults.AccessTokenExpiresName);

        if (string.IsNullOrEmpty(refreshToken)
            || !DateTimeOffset.TryParse(expiresText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var accessExpires))
        {
            await RejectAsync(context).ConfigureAwait(false);
            return;
        }

        if (timeProvider.GetUtcNow() < accessExpires - BffDefaults.RefreshMargin)
        {
            return;
        }

        var cancellation = context.HttpContext.RequestAborted;
        var refreshed = await coordinator.RefreshAsync(
            refreshToken,
            () => authApi.RefreshAsync(new RefreshTokenRequest(refreshToken), cancellation)).ConfigureAwait(false);

        if (refreshed.IsUnavailable)
        {
            // An API outage is not a revocation: keep the session; the next request tries again.
            LogRefreshDeferred(logger);
            return;
        }

        if (!refreshed.IsSuccess || refreshed.Value is null)
        {
            LogRefreshRejected(logger, (int)refreshed.StatusCode);
            await RejectAsync(context).ConfigureAwait(false);
            return;
        }

        AdminSignInService.StoreTokens(properties, refreshed.Value);
        properties.ExpiresUtc = refreshed.Value.SessionExpiresAtUtc;
        context.ShouldRenew = true;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(BffDefaults.Scheme).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin session refresh rejected by the API (status {StatusCode}); signing out")]
    private static partial void LogRefreshRejected(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin session refresh deferred: Onyx.API unavailable")]
    private static partial void LogRefreshDeferred(ILogger logger);
}
