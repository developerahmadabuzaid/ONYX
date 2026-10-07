namespace Onyx.Application.Analytics;

/// <summary>
/// Onyx's conversion events (architecture.md §15), sent from the browser only with analytics consent. Only workflows that
/// exist today; parameters never carry personal data. Names are stable: never rename a published event.
/// </summary>
public static class ConversionEvents
{
    /// <summary>A visitor submitted an inquiry form (contact, start a project, consultation request …).</summary>
    public const string InquirySubmitted = "inquiry_submitted";

    /// <summary>A visitor booked a consultation slot (the booking is confirmed when created).</summary>
    public const string ConsultationBookingConfirmed = "consultation_booking_confirmed";

    public static IReadOnlyList<string> All { get; } = [InquirySubmitted, ConsultationBookingConfirmed];
}
