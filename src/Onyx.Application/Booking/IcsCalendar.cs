using System.Globalization;
using System.Text;

namespace Onyx.Application.Booking;

/// <summary>
/// Minimal iCalendar (RFC 5545) file for one consultation, in UTC. The UID stays the same across reschedules (it is derived from
/// the original inquiry) and SEQUENCE increases, so calendar apps update the existing event instead of adding a second one.
/// No location or meeting link: the consultation format is still an open business decision.
/// </summary>
public static class IcsCalendar
{
    public const string ContentType = "text/calendar";
    public const string FileName = "onyx-consultation.ics";

    public static string Build(Guid uidSource, string summary, DateTimeOffset startUtc, DateTimeOffset endUtc, DateTimeOffset nowUtc, bool cancelled)
    {
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Onyx Vanguard Solutions//Consultation booking//EN",
            "CALSCALE:GREGORIAN",
            cancelled ? "METHOD:CANCEL" : "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:{uidSource:N}@consultation.onyx",
            $"SEQUENCE:{(nowUtc.ToUnixTimeSeconds() / 60).ToString(CultureInfo.InvariantCulture)}",
            $"DTSTAMP:{Format(nowUtc)}",
            $"DTSTART:{Format(startUtc)}",
            $"DTEND:{Format(endUtc)}",
            $"SUMMARY:{Escape(summary)}",
            cancelled ? "STATUS:CANCELLED" : "STATUS:CONFIRMED",
            "END:VEVENT",
            "END:VCALENDAR",
        };

        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            Fold(builder, line);
        }

        return builder.ToString();
    }

    private static string Format(DateTimeOffset value) => value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(";", "\\;", StringComparison.Ordinal).Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>Lines longer than 75 octets are folded (CRLF + space), never inside a UTF-8 sequence.</summary>
    private static void Fold(StringBuilder builder, string line)
    {
        var octets = 0;
        var limit = 75;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (octets + size > limit)
            {
                builder.Append("\r\n ");
                octets = 0;
                limit = 74;
            }

            builder.Append(rune.ToString());
            octets += size;
        }

        builder.Append("\r\n");
    }
}
