using System.Globalization;
using Microsoft.Extensions.Localization;
using Onyx.Web.Resources;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>
/// Dates for the workspace screens: always the Gregorian calendar (also in Arabic, so dates match the rest of the Admin), short
/// day/month forms for charts and cards, and the page direction for charts that run along a time axis.
/// </summary>
public static class DeliveryFormat
{
    public static CultureInfo Culture()
    {
        var culture = (CultureInfo)CultureInfo.CurrentUICulture.Clone();
        if (culture.DateTimeFormat.Calendar is not GregorianCalendar && culture.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault() is { } gregorian)
        {
            culture.DateTimeFormat.Calendar = gregorian;
        }

        return culture;
    }

    public static bool IsRtl => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

    public static string Day(DateOnly date) => date.ToDateTime(TimeOnly.MinValue).ToString("d MMM", Culture());

    public static string DayLong(DateOnly date) => date.ToDateTime(TimeOnly.MinValue).ToString("d MMMM yyyy", Culture());

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Month(DateOnly date) => date.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", Culture());

    /// <summary>A change between two real counts, or null when there is nothing to compare with.</summary>
    public static int? ChangePercent(int current, int previous) =>
        previous == 0 ? null : (int)Math.Round(100.0 * (current - previous) / previous);

    /// <summary>Minutes as a short localized duration ("3h 20m" / "3 س 20 د") for time logs and estimates.</summary>
    public static string Duration(IStringLocalizer<SharedResource> t, int minutes)
    {
        ArgumentNullException.ThrowIfNull(t);
        return minutes < 60 ? t["Delivery.Time.Short.M", minutes]
            : minutes % 60 == 0 ? t["Delivery.Time.Short.H", minutes / 60]
            : t["Delivery.Time.Short.HM", minutes / 60, minutes % 60];
    }

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }

    /// <summary>A stable avatar colour for a person (decorative; the name is always shown or announced).</summary>
    public static string Tone(Guid id) => new[] { "teal", "sky", "violet", "amber", "rose", "green" }[(id.GetHashCode() & 0x7fffffff) % 6];
}
