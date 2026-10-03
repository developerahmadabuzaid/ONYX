namespace Onyx.Infrastructure.Identity;

/// <summary>The only claims carried by access tokens. Permissions are deliberately absent (resolved server-side per request).</summary>
public static class OnyxClaimTypes
{
    public const string Subject = "sub";
    public const string Session = "sid";
    public const string SecurityStampHash = "sst";
    public const string TokenId = "jti";
}
