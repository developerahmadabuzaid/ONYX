using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Brand;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Brand kit foundation. Starts empty and stays empty until the owner enters real values; missing items are listed, never guessed.
/// Colors are entered one per line as <c>Name | #RRGGBB | usage</c>, typefaces as <c>Role | Family | notes</c>.
/// </summary>
[Route("admin/brand-kit")]
public sealed class BrandKitController(IAdminCmsApiClient cms) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await cms.GetBrandKitAsync(cancellationToken);
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("")]
    public async Task<IActionResult> Update(
        Guid? officialLogoMediaId, string? websiteUrl, string? companyEmail, string? colors, string? typefaces, bool allowBrandingOnFeaturedImages,
        bool allowBrandingOnSocialImages, string? imageGenerationNotes, uint version, CancellationToken cancellationToken)
    {
        var colorList = Lines(colors).Select(parts => new BrandColorDto(parts[0], parts.ElementAtOrDefault(1) ?? string.Empty, Blank(parts.ElementAtOrDefault(2)))).ToList();
        var typefaceList = Lines(typefaces).Select(parts => new BrandTypefaceDto(parts[0], parts.ElementAtOrDefault(1) ?? string.Empty, Blank(parts.ElementAtOrDefault(2)))).ToList();
        var request = new UpdateBrandKitRequest(
            officialLogoMediaId, Blank(websiteUrl), Blank(companyEmail), colorList, typefaceList, allowBrandingOnFeaturedImages, allowBrandingOnSocialImages,
            Blank(imageGenerationNotes), version);
        var result = await cms.UpdateBrandKitAsync(request, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)));
    }

    private static IEnumerable<string[]> Lines(string? value) =>
        (value ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|', StringSplitOptions.TrimEntries));
}
