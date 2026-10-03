namespace Onyx.Web.Authentication;

public static class BffDefaults
{
    public const string Scheme = "OnyxAdmin";

    /// <summary><c>__Host-</c> prefix: the browser enforces Secure, Path=/ and no Domain attribute.</summary>
    public const string CookieName = "__Host-onyx.admin";

    public const string AccessTokenName = "access_token";
    public const string RefreshTokenName = "refresh_token";
    public const string AccessTokenExpiresName = "access_expires_at";

    /// <summary>Refresh this long before the access token expires.</summary>
    public static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);
}
