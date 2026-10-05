namespace Onyx.Domain.Leads;

/// <summary>One marketing touch: where the visitor landed, what referred them and the campaign tags of that visit.</summary>
public sealed record TouchPoint(
    string? LandingPath,
    string? Referrer,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? UtmContent,
    string? UtmTerm,
    DateTimeOffset? AtUtc)
{
    public const int PathMaxLength = 500;
    public const int TagMaxLength = 150;

    /// <summary>Keeps only well-formed, bounded values; anything else is dropped rather than stored.</summary>
    public static TouchPoint? Clean(TouchPoint? touch)
    {
        if (touch is null)
        {
            return null;
        }

        var cleaned = new TouchPoint(
            Path(touch.LandingPath), ReferrerOrigin(touch.Referrer), Tag(touch.UtmSource), Tag(touch.UtmMedium), Tag(touch.UtmCampaign), Tag(touch.UtmContent),
            Tag(touch.UtmTerm), touch.AtUtc);
        return cleaned with { AtUtc = null } == new TouchPoint(null, null, null, null, null, null, null, null) ? null : cleaned;
    }

    /// <summary>A site path (no scheme or host), so attribution never stores another site's URL as a landing page.</summary>
    internal static string? Path(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is { Length: > 0 and <= PathMaxLength } && trimmed[0] == '/' && !trimmed.StartsWith("//", StringComparison.Ordinal)
            && !trimmed.Any(char.IsControl)
            ? trimmed
            : null;
    }

    /// <summary>Only the referring site's origin is kept (no path or query, which can carry personal data).</summary>
    private static string? ReferrerOrigin(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;

    private static string? Tag(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is { Length: > 0 and <= TagMaxLength } && !trimmed.Any(char.IsControl) ? trimmed : null;
    }
}
