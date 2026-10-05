using System.Globalization;
using Onyx.Application.Email;
using Onyx.Application.Leads;
using Onyx.Contracts.Leads;
using Onyx.Domain.Booking;

namespace Onyx.Application.Booking;

/// <summary>
/// Builds the visitor booking emails (queued in the same transaction as the booking change, because only that request holds the
/// raw manage token). Dates use Western digits and the Gregorian calendar in every language, with the time zone stated.
/// </summary>
public sealed class BookingMessages(LeadSettings settings)
{
    public const string ConfirmedTemplate = "bookings.confirmed";
    public const string CancelledTemplate = "bookings.cancelled";
    public const string InternalTemplate = "bookings.internal";

    private static readonly IReadOnlyDictionary<string, string> CalendarSummaries = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "استشارة مع Onyx Vanguard Solutions",
        ["en"] = "Consultation with Onyx Vanguard Solutions",
    };

    public static string CalendarSummary(string languageCode) =>
        CalendarSummaries.TryGetValue(languageCode, out var summary) ? summary : CalendarSummaries["en"];

    public static string CalendarFile(ConsultationBooking booking, DateTimeOffset now) =>
        IcsCalendar.Build(booking.InquiryId, CalendarSummary(booking.LanguageCode), booking.StartUtc, booking.EndUtc, now, booking.Status == BookingStatus.Cancelled);

    /// <summary>Date, time and zone values shared by every booking email.</summary>
    public static Dictionary<string, string> SlotValues(ConsultationBooking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);
        var local = TimeZoneInfo.TryFindSystemTimeZoneById(booking.TimeZoneId, out var zone) ? TimeZoneInfo.ConvertTime(booking.StartUtc, zone) : booking.StartUtc;
        var offset = local.Offset;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["date"] = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["time"] = local.ToString("HH:mm", CultureInfo.InvariantCulture),
            ["timezone"] = string.Create(CultureInfo.InvariantCulture, $"{booking.TimeZoneId} (UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset:hh\\:mm})"),
            ["minutes"] = ((int)(booking.EndUtc - booking.StartUtc).TotalMinutes).ToString(CultureInfo.InvariantCulture),
        };
    }

    public EmailRequest Confirmed(ConsultationBooking booking, string toAddress, string toName, string rawManageToken, DateTimeOffset now)
    {
        var model = SlotValues(booking);
        model["name"] = toName;
        model["reference"] = InquiryRecorder.Reference(booking.InquiryId);
        if (ManageLink(booking.LanguageCode, rawManageToken) is { } link)
        {
            model["link"] = link.AbsoluteUri;
        }

        return new EmailRequest(
            ConfirmedTemplate, booking.LanguageCode, toAddress, toName, model,
            [new EmailAttachment(IcsCalendar.FileName, IcsCalendar.ContentType, CalendarFile(booking, now))]);
    }

    public static EmailRequest Cancelled(ConsultationBooking booking, string toAddress, string toName, DateTimeOffset now)
    {
        var model = SlotValues(booking);
        model["name"] = toName;
        model["reference"] = InquiryRecorder.Reference(booking.InquiryId);
        return new EmailRequest(
            CancelledTemplate, booking.LanguageCode, toAddress, toName, model,
            [new EmailAttachment(IcsCalendar.FileName, IcsCalendar.ContentType, CalendarFile(booking, now))]);
    }

    /// <summary>The visitor page that cancels or reschedules; null when the public site origin is not configured.</summary>
    public Uri? ManageLink(string languageCode, string rawManageToken)
    {
        if (settings.PublicBaseUrl is not { } origin || !LeadRouteSegments.ManageBooking.TryGetValue(languageCode, out var segment))
        {
            return null;
        }

        var path = "/" + languageCode + "/" + Uri.EscapeDataString(segment);
        return new Uri(origin, path + "?token=" + Uri.EscapeDataString(rawManageToken));
    }
}
