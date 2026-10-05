namespace Onyx.Infrastructure.Privacy;

/// <summary>Bound from the <c>Privacy</c> section.</summary>
public sealed class PrivacyOptions
{
    public const string SectionName = "Privacy";

    /// <summary>
    /// Optional secret (base64, ≥ 32 bytes) for HMAC-SHA256 IP hashing in privacy records. When absent, IP addresses are not stored.
    /// </summary>
    public string? IpHashKey { get; set; }

    /// <summary>Retention job: records per transaction (technical sizing; retention periods come from saved policies).</summary>
    public int RetentionBatchSize { get; set; } = 200;

    /// <summary>Retention job: batches per category per run; the remainder waits for the next run.</summary>
    public int RetentionMaxBatchesPerRun { get; set; } = 50;

    public static bool IsValidKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return true;
        }

        var buffer = new byte[key.Length];
        return Convert.TryFromBase64String(key, buffer, out var length) && length >= 32;
    }
}
