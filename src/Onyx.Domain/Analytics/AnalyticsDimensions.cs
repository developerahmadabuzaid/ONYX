using System.Security.Cryptography;
using System.Text;

namespace Onyx.Domain.Analytics;

/// <summary>Bounded dimension values for provider aggregates: trimmed, length-capped, never null, and hashed into a row key.</summary>
public static class AnalyticsDimensions
{
    /// <summary>Rows beyond the per-day cap are summed into one row with this value.</summary>
    public const string Other = "(other)";

    /// <summary>The provider gave no value for this dimension.</summary>
    public const string NotSet = "(not set)";

    public const int PathMaxLength = 500;
    public const int ShortMaxLength = 100;
    public const int CampaignMaxLength = 150;
    public const int QueryMaxLength = 300;
    public const int HashLength = 64;

    public static string Clean(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return NotSet;
        }

        var single = string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return single.Length > maxLength ? single[..maxLength] : single;
    }

    /// <summary>SHA-256 (hex) of the report kind and dimension values: the unique key that makes re-syncing a day idempotent.</summary>
    public static string Hash(string kind, params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var key = string.Join('\u001f', values.Prepend(kind));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }
}
