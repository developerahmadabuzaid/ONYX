using System.Security.Cryptography;
using System.Text;

namespace Onyx.Infrastructure.Identity;

/// <summary>
/// Encrypts a successor refresh token with AES-256-GCM under a key derived (HKDF-SHA256) from the predecessor's raw value.
/// Only a client presenting the predecessor can open it; the database holds neither raw token.
/// </summary>
public static class RefreshSuccessorSealer
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] Info = Encoding.UTF8.GetBytes("onyx/refresh-successor/v1");

    public static string Seal(string predecessorRawToken, Guid predecessorId, string successorRawToken)
    {
        var key = DeriveKey(predecessorRawToken, predecessorId);
        var plaintext = Encoding.UTF8.GetBytes(successorRawToken);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        CryptographicOperations.ZeroMemory(key);
        return Convert.ToBase64String([.. nonce, .. tag, .. ciphertext]);
    }

    public static string? TryOpen(string predecessorRawToken, Guid predecessorId, string sealedSuccessor)
    {
        try
        {
            var payload = Convert.FromBase64String(sealedSuccessor);
            if (payload.Length <= NonceSize + TagSize)
            {
                return null;
            }

            var key = DeriveKey(predecessorRawToken, predecessorId);
            var plaintext = new byte[payload.Length - NonceSize - TagSize];
            using (var aes = new AesGcm(key, TagSize))
            {
                aes.Decrypt(payload.AsSpan(0, NonceSize), payload.AsSpan(NonceSize + TagSize), payload.AsSpan(NonceSize, TagSize), plaintext);
            }

            CryptographicOperations.ZeroMemory(key);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return null;
        }
    }

    private static byte[] DeriveKey(string predecessorRawToken, Guid predecessorId) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(predecessorRawToken), 32, predecessorId.ToByteArray(), Info);
}
