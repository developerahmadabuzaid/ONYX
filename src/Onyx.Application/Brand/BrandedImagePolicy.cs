using Onyx.Domain.Brand;
using Onyx.Domain.Common;

namespace Onyx.Application.Brand;

/// <summary>
/// Decides what branding a future generated image may carry. Featured and social images may use the official logo, website
/// URL and company email from the Brand Kit (when the kit allows it); article body images never carry promotional branding.
/// Missing brand inputs mean "needs information", never an invented substitute or a replacement logo.
/// </summary>
public static class BrandedImagePolicy
{
    public static Result<BrandingInstructions?> ForPurpose(ImagePurpose purpose, BrandKit brandKit)
    {
        ArgumentNullException.ThrowIfNull(brandKit);
        var allowed = purpose switch
        {
            ImagePurpose.Featured => brandKit.AllowBrandingOnFeaturedImages,
            ImagePurpose.Social => brandKit.AllowBrandingOnSocialImages,
            _ => false,
        };

        if (!allowed)
        {
            return Result.Success<BrandingInstructions?>(null);
        }

        if (brandKit.OfficialLogoMediaId is not { } logo)
        {
            return Error.Validation("Brand.NeedsInformation", "Upload the official Onyx logo to the Brand Kit before generating branded images.");
        }

        return new BrandingInstructions(logo, brandKit.WebsiteUrl, brandKit.CompanyEmail, brandKit.Colors.Select(color => color.Hex).ToList(), brandKit.ImageGenerationNotes);
    }
}
