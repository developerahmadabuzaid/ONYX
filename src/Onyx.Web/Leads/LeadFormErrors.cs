using Onyx.Contracts.Common;

namespace Onyx.Web.Leads;

/// <summary>Maps API problem codes to visitor-facing resource keys; anything unknown gets a generic message.</summary>
public static class LeadFormErrors
{
    private static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Captcha.Failed"] = "Form.Error.Captcha",
        ["Captcha.Unavailable"] = "Form.Error.Unavailable",
        ["Inquiry.InvalidEmail"] = "Form.InvalidEmail",
        ["Inquiry.InvalidPhone"] = "Form.InvalidPhone",
        ["Inquiry.MessageRequired"] = "Form.Error.MessageRequired",
        ["Inquiry.ContactConsentRequired"] = "Form.ConsentRequired",
        ["Inquiry.InvalidContext"] = "Form.Error.Context",
        ["Booking.SlotUnavailable"] = "Booking.Error.SlotTaken",
        ["Booking.Closed"] = "Booking.Error.Closed",
        ["Booking.SameSlot"] = "Booking.Error.SameSlot",
        ["Booking.NotManageable"] = "Booking.Error.NotManageable",
        ["Booking.LinkInvalid"] = "Booking.Error.LinkInvalid",
    };

    public static string KeyFor(ApiProblem? problem, bool unavailable) =>
        unavailable ? "Form.Error.Unavailable"
        : problem?.Code is { } code && Keys.TryGetValue(code, out var key) ? key
        : problem?.Status == StatusCodes.Status429TooManyRequests ? "Form.Error.TooMany"
        : problem?.Status == StatusCodes.Status503ServiceUnavailable ? "Form.Error.Unavailable"
        : "Form.Error.Generic";
}
