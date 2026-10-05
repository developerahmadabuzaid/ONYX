namespace Onyx.Contracts.Leads;

/// <summary>
/// Localized public paths of the conversion pages, shared by Onyx.Web (routing) and the API/Worker (links in emails).
/// Changing a value breaks links already sent by email.
/// </summary>
public static class LeadRouteSegments
{
    public static IReadOnlyDictionary<string, string> StartProject { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "ابدأ-مشروعك",
        ["en"] = "start-a-project",
    };

    public static IReadOnlyDictionary<string, string> BookConsultation { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "احجز-استشارة",
        ["en"] = "book-a-consultation",
    };

    public static IReadOnlyDictionary<string, string> Contact { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "تواصل-معنا",
        ["en"] = "contact",
    };

    public static IReadOnlyDictionary<string, string> ManageBooking { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "إدارة-الحجز",
        ["en"] = "manage-booking",
    };

    public static IEnumerable<string> All =>
        StartProject.Values.Concat(BookConsultation.Values).Concat(Contact.Values).Concat(ManageBooking.Values);
}
