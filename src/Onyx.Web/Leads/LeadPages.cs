using Onyx.Contracts.Leads;
using Onyx.Web.Localization;

namespace Onyx.Web.Leads;

/// <summary>The conversion pages: their localized routes and paths, from the segments shared with the API.</summary>
public static class LeadPages
{
    public const string StartProject = "lead.start-project";
    public const string Contact = "lead.contact";
    public const string BookConsultation = "lead.book-consultation";
    public const string ManageBooking = "lead.manage-booking";

    /// <summary>Turnstile actions; Onyx.API checks the token was solved for the same action (InquiryService / BookingService).</summary>
    public const string InquiryCaptchaAction = "inquiry";
    public const string BookingCaptchaAction = "booking";

    public static IEnumerable<LocalizedRoute> Routes =>
    [
        new(StartProject, "LeadForms", "StartProject", LeadRouteSegments.StartProject),
        new(Contact, "LeadForms", "Contact", LeadRouteSegments.Contact),
        new(BookConsultation, "Consultation", "Book", LeadRouteSegments.BookConsultation),
        new(ManageBooking, "Consultation", "Manage", LeadRouteSegments.ManageBooking),
    ];

    /// <summary>Language → path of the page in every language (for hreflang and the language switcher).</summary>
    public static IReadOnlyDictionary<string, string> Alternates(LocalizedRouteTable routes, string key, IEnumerable<string> languages, params string[] slugs)
    {
        ArgumentNullException.ThrowIfNull(routes);
        return languages
            .Select(language => (language, path: routes.PathFor(key, language, slugs)))
            .Where(entry => entry.path is not null)
            .ToDictionary(entry => entry.language, entry => entry.path!, StringComparer.Ordinal);
    }
}
