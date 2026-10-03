using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace Onyx.API.IntegrationTests.Identity;

/// <summary>Independent RFC 6238 implementation (not the production one) so tests act like a real authenticator app.</summary>
public static class TestTotp
{
    public static string Code(string base32Key, DateTimeOffset at)
    {
        var key = Base32(base32Key);
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, at.ToUnixTimeSeconds() / 30);
        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var value = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] Base32(string input)
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = string.Concat(input.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant().TrimEnd('=')
            .Select(character => Convert.ToString(Alphabet.IndexOf(character, StringComparison.Ordinal), 2).PadLeft(5, '0')));
        return Enumerable.Range(0, bits.Length / 8).Select(index => Convert.ToByte(bits.Substring(index * 8, 8), 2)).ToArray();
    }
}
