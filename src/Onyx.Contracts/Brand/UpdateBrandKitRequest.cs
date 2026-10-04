using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Brand;

public sealed record UpdateBrandKitRequest(
    Guid? OfficialLogoMediaId,
    string? WebsiteUrl,
    string? CompanyEmail,
    IReadOnlyList<BrandColorDto> Colors,
    IReadOnlyList<BrandTypefaceDto> Typefaces,
    bool AllowBrandingOnFeaturedImages,
    bool AllowBrandingOnSocialImages,
    string? ImageGenerationNotes,
    uint Version);
