using System.Net.Mail;
using System.Text.RegularExpressions;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Brand;

/// <summary>
/// The single Onyx Brand Kit. Starts empty: values are entered only from owner-supplied brand material, never invented.
/// The official logo is the real Onyx logo uploaded to the media library; no replacement logo is ever generated.
/// </summary>
public sealed partial class BrandKit : AuditableEntity, IAuditable
{
    public const int MaxColors = 20;
    public const int MaxTypefaces = 10;
    public const int UrlMaxLength = 200;
    public const int EmailMaxLength = 254;
    public const int TextMaxLength = 500;
    public const int RulesNotesMaxLength = 4_000;

    private BrandKit()
    {
        Colors = [];
        Typefaces = [];
    }

    public Guid? OfficialLogoMediaId { get; private set; }

    public string? WebsiteUrl { get; private set; }

    public string? CompanyEmail { get; private set; }

    public IReadOnlyList<BrandColor> Colors { get; private set; }

    public IReadOnlyList<BrandTypeface> Typefaces { get; private set; }

    /// <summary>Featured and social images may carry the logo, website URL and company email (Brand Kit values only).</summary>
    public bool AllowBrandingOnFeaturedImages { get; private set; } = true;

    public bool AllowBrandingOnSocialImages { get; private set; } = true;

    /// <summary>Additional editorial rules for future image generation. Article body images never carry promotional branding.</summary>
    public string? ImageGenerationNotes { get; private set; }

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Content;

    public static BrandKit CreateEmpty() => new();

    public Result Update(
        Guid? officialLogoMediaId,
        string? websiteUrl,
        string? companyEmail,
        IReadOnlyList<BrandColor> colors,
        IReadOnlyList<BrandTypeface> typefaces,
        bool allowBrandingOnFeaturedImages,
        bool allowBrandingOnSocialImages,
        string? imageGenerationNotes)
    {
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(typefaces);
        var url = Clean(websiteUrl);
        var email = Clean(companyEmail);

        if (url is not null && (url.Length > UrlMaxLength || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            return Invalid("The website URL must be an absolute https URL.");
        }

        if (email is not null && (email.Length > EmailMaxLength || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email))
        {
            return Invalid("The company email is not a valid address.");
        }

        if (colors.Count > MaxColors || colors.Any(color => color is null || !HexColor().IsMatch(color.Hex) || string.IsNullOrWhiteSpace(color.Name)
            || color.Name.Length > 100 || color.Usage is { Length: > TextMaxLength }))
        {
            return Invalid($"Up to {MaxColors} colours, each with a name and a #RRGGBB value.");
        }

        if (typefaces.Count > MaxTypefaces || typefaces.Any(typeface => typeface is null || string.IsNullOrWhiteSpace(typeface.Role) || string.IsNullOrWhiteSpace(typeface.Family)
            || typeface.Role.Length > 100 || typeface.Family.Length > 100 || typeface.Notes is { Length: > TextMaxLength }))
        {
            return Invalid($"Up to {MaxTypefaces} typefaces, each with a role and a family.");
        }

        if (imageGenerationNotes is { Length: > RulesNotesMaxLength })
        {
            return Invalid($"Image rules are limited to {RulesNotesMaxLength} characters.");
        }

        OfficialLogoMediaId = officialLogoMediaId;
        WebsiteUrl = url;
        CompanyEmail = email;
        Colors = [.. colors];
        Typefaces = [.. typefaces];
        AllowBrandingOnFeaturedImages = allowBrandingOnFeaturedImages;
        AllowBrandingOnSocialImages = allowBrandingOnSocialImages;
        ImageGenerationNotes = Clean(imageGenerationNotes);
        return Result.Success();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Error Invalid(string message) => Error.Validation("BrandKit.Invalid", message);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColor();
}
