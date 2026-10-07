using System.Text.RegularExpressions;

namespace Onyx.Web.Configuration;

/// <summary>
/// Bound from <c>Analytics</c>. The GA4 browser tag is off until the owner sets the property's measurement id (public, not a
/// secret). Even then it loads only for visitors who accepted analytics cookies, never on admin or token pages.
/// </summary>
public sealed partial class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    /// <summary>GA4 web stream measurement id (<c>G-…</c>); empty means not configured.</summary>
    public string? Ga4MeasurementId { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Ga4MeasurementId);

    public static bool IsValidMeasurementId(string? value) => string.IsNullOrEmpty(value) || MeasurementIdPattern().IsMatch(value);

    [GeneratedRegex("^G-[A-Z0-9]{4,20}$", RegexOptions.CultureInvariant)]
    private static partial Regex MeasurementIdPattern();
}
