using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Brand;

/// <summary><see cref="MissingItems"/> lists brand inputs not yet supplied (nothing is ever filled in automatically).</summary>
public sealed record BrandKitResponse(
    Guid? OfficialLogoMediaId,
    string? WebsiteUrl,
    string? CompanyEmail,
    IReadOnlyList<BrandColorDto> Colors,
    IReadOnlyList<BrandTypefaceDto> Typefaces,
    bool AllowBrandingOnFeaturedImages,
    bool AllowBrandingOnSocialImages,
    string? ImageGenerationNotes,
    IReadOnlyList<string> MissingItems,
    uint Version);
