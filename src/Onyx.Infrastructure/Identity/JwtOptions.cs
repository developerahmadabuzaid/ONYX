using System.ComponentModel.DataAnnotations;

namespace Onyx.Infrastructure.Identity;

/// <summary>Bound from the <c>Jwt</c> section. <see cref="SigningKey"/> is a secret (user secrets / environment), never committed.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Base64-encoded HMAC-SHA256 key of at least 32 random bytes.</summary>
    [Required]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 30)]
    public int AccessTokenMinutes { get; set; } = 10;

    [Range(0, 120)]
    public int ClockSkewSeconds { get; set; } = 30;

    public byte[] GetSigningKeyBytes() => Convert.FromBase64String(SigningKey);
}
