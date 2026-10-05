using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

/// <summary>
/// Lead runtime settings, bound from configuration by the host (<c>Leads</c> section).
/// </summary>
/// <param name="ConsentVersion">Identifies the form consent wording the visitor saw; bump it when that wording changes.</param>
/// <param name="DefaultRecipients">Internal notification addresses for every inquiry type without its own list.</param>
/// <param name="RecipientsByType">Per-type internal notification addresses (e.g. consultations to the sales inbox).</param>
/// <param name="AdminBaseUrl">Onyx.Web admin origin, for the lead link in internal emails; null leaves the link out.</param>
/// <param name="PublicBaseUrl">Public site origin, for the visitor booking-management link; null leaves the link out.</param>
/// <param name="ManageLinkLifetime">How long after the slot a booking-management link stays usable for the calendar file.</param>
public sealed record LeadSettings(
    string ConsentVersion,
    IReadOnlyList<string> DefaultRecipients,
    IReadOnlyDictionary<InquiryType, IReadOnlyList<string>> RecipientsByType,
    Uri? AdminBaseUrl,
    Uri? PublicBaseUrl,
    TimeSpan ManageLinkLifetime)
{
    public static LeadSettings Default { get; } = new("1", [], new Dictionary<InquiryType, IReadOnlyList<string>>(), null, null, TimeSpan.FromDays(1));

    public IReadOnlyList<string> RecipientsFor(InquiryType type) =>
        RecipientsByType.TryGetValue(type, out var recipients) && recipients.Count > 0 ? recipients : DefaultRecipients;
}
