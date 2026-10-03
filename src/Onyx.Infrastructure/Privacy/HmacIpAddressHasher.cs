using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Onyx.Application.Privacy;

namespace Onyx.Infrastructure.Privacy;

public sealed class HmacIpAddressHasher(IOptions<PrivacyOptions> options) : IIpAddressHasher
{
    public string? Hash(string? ipAddress)
    {
        var key = options.Value.IpHashKey;
        if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        // The key is validated at startup (PrivacyOptions.IsValidKey).
        var keyBytes = Convert.FromBase64String(key);
        return Convert.ToHexString(HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(ipAddress)));
    }
}
