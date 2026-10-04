using Onyx.Application.Brand;
using Onyx.Application.Media;
using Onyx.Contracts.Organization;

namespace Onyx.Application.Seo;

/// <summary>
/// Gate for Organization structured data. Required facts: the company name (fixed), the official logo from the Brand Kit (an
/// existing media asset) and the Brand Kit website, which must be this site's own public origin. The owner must also switch the
/// feature on. Nothing is filled in when a fact is missing: the answer is simply "not available".
/// </summary>
public sealed class OrganizationProfileService(OrganizationProfileSettings settings, IBrandKitRepository brandKits, IMediaRepository media)
    : IOrganizationProfileService
{
    /// <summary>The registered company name (CLAUDE.md §1).</summary>
    public const string CompanyName = "Onyx Vanguard Solutions";

    public async Task<PublicOrganizationResponse?> GetVerifiedAsync(CancellationToken cancellationToken)
    {
        if (!settings.StructuredDataEnabled || !Uri.TryCreate(settings.PublicBaseUrl, UriKind.Absolute, out var site))
        {
            return null;
        }

        var kit = await brandKits.GetAsync(cancellationToken).ConfigureAwait(false);
        if (kit?.OfficialLogoMediaId is not { } logoId || !Uri.TryCreate(kit.WebsiteUrl, UriKind.Absolute, out var website) || !SameOrigin(site, website))
        {
            return null;
        }

        var logo = (await media.GetLookupAsync([logoId], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(logoId);
        if (logo is null)
        {
            return null;
        }

        return new PublicOrganizationResponse(CompanyName, site.GetLeftPart(UriPartial.Authority) + "/", logo.Id, logo.Width, logo.Height, kit.CompanyEmail);
    }

    private static bool SameOrigin(Uri site, Uri website) =>
        string.Equals(site.Scheme, website.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(site.IdnHost, website.IdnHost, StringComparison.OrdinalIgnoreCase)
        && site.Port == website.Port;
}
