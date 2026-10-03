using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Onyx.Infrastructure.Identity;

/// <summary>
/// RFC 6238 TOTP (SHA-1, 6 digits, 30-second steps) with a ±1 step window and replay protection: a code is accepted only
/// for a time step later than the last one used by that account.
/// </summary>
public static class Totp
{
    public const int StepSeconds = 30;
    private const int Digits = 6;

    public static bool TryMatch(byte[] key, string code, DateTimeOffset now, long? lastUsedStep, out long matchedStep)
    {
        ArgumentNullException.ThrowIfNull(key);
        matchedStep = 0;
        if (code is not { Length: Digits } || !code.All(char.IsAsciiDigit))
        {
            return false;
        }

        var currentStep = now.ToUnixTimeSeconds() / StepSeconds;
        var expected = Encoding.ASCII.GetBytes(code);
        var matched = false;

        // Evaluate every candidate step so timing does not reveal which one matched.
        for (var offset = -1; offset <= 1; offset++)
        {
            var step = currentStep + offset;
            var candidate = Encoding.ASCII.GetBytes(Compute(key, step));
            if (CryptographicOperations.FixedTimeEquals(candidate, expected) && (lastUsedStep is null || step > lastUsedStep) && !matched)
            {
                matched = true;
                matchedStep = step;
            }
        }

        return matched;
    }

    public static string Compute(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var hash = HMACSHA1.HashData(key, counter);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    /// <summary>Decodes the RFC 4648 base32 authenticator key produced by ASP.NET Core Identity.</summary>
    public static byte[] DecodeBase32(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = input.Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>(cleaned.Length * 5 / 8);
        int buffer = 0, bits = 0;

        foreach (var character in cleaned)
        {
            var value = Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (value < 0)
            {
                throw new FormatException("Invalid base32 character.");
            }

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)((buffer >> bits) & 0xFF));
            }
        }

        return [.. output];
    }
}
