using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Onyx.Contracts.Identity;
using Onyx.Web.ApiClients;
using Onyx.Web.Localization;

namespace Onyx.Web.Authentication;

/// <summary>
/// Turns an API token pair into the encrypted admin cookie. The ticket holds three small claims plus the tokens; it is
/// encrypted and signed by Data Protection, so tokens never reach browser JavaScript.
/// </summary>
public sealed class AdminSignInService(IAuthApiClient authApi)
{
    public async Task<bool> SignInAsync(HttpContext httpContext, TokenPairResponse tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(tokens);

        var me = await authApi.GetCurrentUserAsync(tokens.AccessToken, cancellationToken).ConfigureAwait(false);
        if (!me.IsSuccess || me.Value is null)
        {
            return false;
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, me.Value.Id.ToString()),
                new Claim(ClaimTypes.Name, me.Value.DisplayName),
                new Claim(OnyxRequestCultureProvider.LanguageClaim, me.Value.PreferredLanguage),
            ],
            BffDefaults.Scheme);

        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = true,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = tokens.SessionExpiresAtUtc,
        };
        StoreTokens(properties, tokens);

        await httpContext.SignInAsync(BffDefaults.Scheme, new ClaimsPrincipal(identity), properties).ConfigureAwait(false);
        return true;
    }

    public static void StoreTokens(AuthenticationProperties properties, TokenPairResponse tokens)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(tokens);
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = BffDefaults.AccessTokenName, Value = tokens.AccessToken },
            new AuthenticationToken { Name = BffDefaults.RefreshTokenName, Value = tokens.RefreshToken },
            new AuthenticationToken { Name = BffDefaults.AccessTokenExpiresName, Value = tokens.AccessTokenExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture) },
        ]);
    }
}
