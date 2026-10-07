using System.ComponentModel.DataAnnotations;

namespace Onyx.API.Configuration;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Applies to every API request, partitioned by client IP.</summary>
    [Required]
    public FixedWindowSettings Global { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };

    /// <summary>Named policy <see cref="RateLimitPolicies.Strict"/> for authentication, password reset, public forms and tools.</summary>
    [Required]
    public FixedWindowSettings Strict { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Named policy <see cref="RateLimitPolicies.Search"/> for public site search (per client IP).</summary>
    [Required]
    public FixedWindowSettings Search { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };
}

public sealed class FixedWindowSettings
{
    [Range(1, 100_000)]
    public int PermitLimit { get; set; }

    [Range(1, 86_400)]
    public int WindowSeconds { get; set; }
}

public static class RateLimitPolicies
{
    public const string Strict = "strict";

    public const string Search = "search";
}
