namespace Onyx.Web.Analytics;

/// <summary>
/// The two conversion events of workflows that exist today (names match the API's <c>ConversionEvents</c>). Carried once in
/// TempData from the successful POST to its confirmation page, so a refresh or repeat view never sends it again.
/// </summary>
public sealed record ConversionEvent(string Name, string? InquiryType)
{
    public const string InquirySubmitted = "inquiry_submitted";
    public const string ConsultationBookingConfirmed = "consultation_booking_confirmed";

    private static readonly HashSet<string> InquiryTypes = new(StringComparer.Ordinal)
    {
        "StartProject", "Contact", "RequestConsultation", "ServiceInquiry", "SimilarProject", "ContextualCta",
    };

    public static string ForInquiry(string inquiryType) => $"{InquirySubmitted}:{inquiryType}";

    public static string ForBooking() => ConsultationBookingConfirmed;

    /// <summary>Only the known names and inquiry types survive; anything else is dropped.</summary>
    public static ConversionEvent? Parse(string? value)
    {
        if (value == ConsultationBookingConfirmed)
        {
            return new ConversionEvent(ConsultationBookingConfirmed, null);
        }

        var parts = value?.Split(':', 2);
        return parts is [InquirySubmitted, var type] && InquiryTypes.Contains(type) ? new ConversionEvent(InquirySubmitted, ToSnake(type)) : null;
    }

    private static string ToSnake(string value) =>
        string.Concat(value.Select((character, index) => char.IsUpper(character) ? (index > 0 ? "_" : string.Empty) + char.ToLowerInvariant(character) : character.ToString()));
}
