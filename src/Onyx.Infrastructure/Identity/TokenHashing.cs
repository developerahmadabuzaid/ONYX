using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Onyx.Infrastructure.Identity;

public static class TokenHashing
{
    /// <summary>256-bit random, URL-safe opaque token.</summary>
    public static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    /// <summary>Lookup hash for opaque tokens (they are high-entropy, so a fast hash is appropriate).</summary>
    public static string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>Short digest of the security stamp for the <c>sst</c> claim; the stamp itself never leaves the server.</summary>
    public static string SecurityStampHash(string? securityStamp) =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp ?? string.Empty)))[..22];
}
