using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Onyx.Application.Content;

/// <summary>256-bit random preview tokens (base64url) and their SHA-256 hashes. Only hashes are stored.</summary>
public static class PreviewTokenCodec
{
    public const int TokenLength = 43;

    public static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    public static bool IsWellFormed(string? token) =>
        token is { Length: TokenLength } && token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
