using System.ComponentModel.DataAnnotations;

namespace Onyx.Application.Products;

/// <summary>
/// Bound from <c>AiDemo</c> and registered as a singleton by the composition root. Off by default: the on-site AI demo accepts requests only when it is switched on here, and the
/// Worker processes them only while an LLM provider is connected. The limits bound abuse and cost.
/// </summary>
public sealed class AiDemoOptions
{
    public const string SectionName = "AiDemo";

    public bool Enabled { get; set; }

    [Range(1, 200)]
    public int PerCustomerDailyLimit { get; set; } = 10;

    [Range(1, 100_000)]
    public int GlobalDailyLimit { get; set; } = 500;

    [Range(64, 2000)]
    public int MaxOutputTokens { get; set; } = 600;
}
