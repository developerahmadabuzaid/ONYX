using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Media;
using Onyx.Contracts.Brand;
using Onyx.Domain.Auditing;
using Onyx.Domain.Brand;
using Onyx.Domain.Common;

namespace Onyx.Application.Brand;

/// <summary>Brand Kit: empty until owner-supplied values are entered. Missing items are reported, never filled in.</summary>
public sealed class BrandKitService(IBrandKitRepository brandKits, IMediaRepository media, IAuditTrail auditTrail, IUnitOfWork unitOfWork) : IBrandKitService
{
    public async Task<BrandKitResponse> GetAsync(CancellationToken cancellationToken) =>
        ToResponse(await brandKits.GetAsync(cancellationToken).ConfigureAwait(false) ?? BrandKit.CreateEmpty());

    public async Task<Result<BrandKitResponse>> UpdateAsync(UpdateBrandKitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OfficialLogoMediaId is { } logoId && await media.GetAsync(logoId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Error.Validation("BrandKit.LogoMissing", "The official logo must be an image uploaded to the media library.");
        }

        var brandKit = await brandKits.GetAsync(cancellationToken).ConfigureAwait(false);
        if (brandKit is null)
        {
            brandKit = BrandKit.CreateEmpty();
            brandKits.Add(brandKit);
        }
        else
        {
            brandKits.SetExpectedVersion(brandKit, request.Version);
        }

        var before = ToResponse(brandKit);
        var updated = brandKit.Update(
            request.OfficialLogoMediaId,
            request.WebsiteUrl,
            request.CompanyEmail,
            (request.Colors ?? []).Select(color => new BrandColor(color.Name?.Trim() ?? string.Empty, color.Hex?.Trim() ?? string.Empty, color.Usage)).ToList(),
            (request.Typefaces ?? []).Select(typeface => new BrandTypeface(typeface.Role?.Trim() ?? string.Empty, typeface.Family?.Trim() ?? string.Empty, typeface.Notes)).ToList(),
            request.AllowBrandingOnFeaturedImages,
            request.AllowBrandingOnSocialImages,
            request.ImageGenerationNotes);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        var after = ToResponse(brandKit);
        auditTrail.Record(AuditAction.BrandKitUpdated, AuditCategory.Content, nameof(BrandKit), brandKit.Id.ToString(), new
        {
            logo = new { from = before.OfficialLogoMediaId, to = after.OfficialLogoMediaId },
            websiteUrl = new { from = before.WebsiteUrl, to = after.WebsiteUrl },
            companyEmail = new { from = before.CompanyEmail, to = after.CompanyEmail },
            colors = after.Colors.Count,
            typefaces = after.Typefaces.Count,
            after.AllowBrandingOnFeaturedImages,
            after.AllowBrandingOnSocialImages,
        });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(brandKit);
    }

    public static IReadOnlyList<string> MissingItems(BrandKit brandKit)
    {
        ArgumentNullException.ThrowIfNull(brandKit);
        var missing = new List<string>();
        if (brandKit.OfficialLogoMediaId is null)
        {
            missing.Add("OfficialLogo");
        }

        if (brandKit.WebsiteUrl is null)
        {
            missing.Add("WebsiteUrl");
        }

        if (brandKit.CompanyEmail is null)
        {
            missing.Add("CompanyEmail");
        }

        if (brandKit.Colors.Count == 0)
        {
            missing.Add("Colors");
        }

        if (brandKit.Typefaces.Count == 0)
        {
            missing.Add("Typefaces");
        }

        return missing;
    }

    private static BrandKitResponse ToResponse(BrandKit brandKit) => new(
        brandKit.OfficialLogoMediaId,
        brandKit.WebsiteUrl,
        brandKit.CompanyEmail,
        brandKit.Colors.Select(color => new BrandColorDto(color.Name, color.Hex, color.Usage)).ToList(),
        brandKit.Typefaces.Select(typeface => new BrandTypefaceDto(typeface.Role, typeface.Family, typeface.Notes)).ToList(),
        brandKit.AllowBrandingOnFeaturedImages,
        brandKit.AllowBrandingOnSocialImages,
        brandKit.ImageGenerationNotes,
        MissingItems(brandKit),
        brandKit.Version);
}
