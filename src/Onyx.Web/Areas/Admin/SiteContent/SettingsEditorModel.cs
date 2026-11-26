using Onyx.Contracts.SiteContent;

namespace Onyx.Web.Areas.Admin.SiteContent;

/// <summary>
/// The website settings editor. Fields start empty and are never pre-filled with guesses; the only pre-filled rows are the verified
/// offices (Riyadh – Granada District, Amman) the public site already shows, so saving keeps them.
/// </summary>
public sealed record SettingsEditorModel(
    SiteSettingsContent Current,
    SiteContentDocumentResponse Document,
    IReadOnlyList<SiteContentRevisionResponse> Revisions,
    IReadOnlyList<string> Languages,
    IReadOnlyList<SiteOffice> VerifiedOffices)
{
    public const int PhoneRows = 3;
    public const int OfficeRows = 4;
    public const int SocialRows = 6;

    /// <summary>Saved offices, else the verified code-owned ones.</summary>
    public IReadOnlyList<SiteOffice> Offices => Current.Offices.Count > 0 ? Current.Offices : VerifiedOffices;

    public bool OfficesFromCode => Current.Offices.Count == 0;

    public static string Text(IReadOnlyDictionary<string, string>? values, string language) => values?.GetValueOrDefault(language) ?? string.Empty;
}
