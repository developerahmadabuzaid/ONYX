using FluentValidation;
using Onyx.Contracts.SiteContent;

namespace Onyx.Application.SiteContent;

/// <summary>Website settings hold verified contact details only: well-formed values, https profile links, bounded lists.</summary>
internal sealed class SiteSettingsContentValidator : AbstractValidator<SiteSettingsContent>
{
    public const int MaxPhones = 5;
    public const int MaxOffices = 6;
    public const int MaxSocialLinks = 8;

    private static readonly HashSet<string> Languages = new(StringComparer.Ordinal) { "ar", "en" };

    public SiteSettingsContentValidator()
    {
        RuleFor(settings => settings.CompanyName).Must(BeLocalized(100)).WithMessage("Company name: Arabic/English text up to 100 characters.");
        RuleFor(settings => settings.ContactEmail).EmailAddress().MaximumLength(254).When(settings => !string.IsNullOrEmpty(settings.ContactEmail));
        RuleFor(settings => settings.Phones).Must(phones => phones.Count <= MaxPhones).WithMessage($"At most {MaxPhones} phone numbers.");
        RuleForEach(settings => settings.Phones).ChildRules(phone =>
        {
            phone.RuleFor(item => item.Number).NotEmpty().MaximumLength(40).Matches(@"^\+?[0-9][0-9 ()\-]{4,38}$").WithMessage("Phone numbers may contain digits, spaces, +, - and parentheses.");
            phone.RuleFor(item => item.Label).Must(BeLocalized(60)).WithMessage("Phone label: Arabic/English text up to 60 characters.");
        });
        RuleFor(settings => settings.Offices).Must(offices => offices.Count <= MaxOffices).WithMessage($"At most {MaxOffices} offices.");
        RuleForEach(settings => settings.Offices).ChildRules(office =>
        {
            office.RuleFor(item => item.Name).Must(BeLocalized(100)).Must(value => value.Values.Any(text => !string.IsNullOrWhiteSpace(text)))
                .WithMessage("Each office needs a name (Arabic/English, up to 100 characters).");
            office.RuleFor(item => item.Address).Must(BeLocalized(300)).WithMessage("Office address: Arabic/English text up to 300 characters.");
        });
        RuleFor(settings => settings.SocialLinks).Must(links => links.Count <= MaxSocialLinks).WithMessage($"At most {MaxSocialLinks} social links.");
        RuleForEach(settings => settings.SocialLinks).ChildRules(link =>
        {
            link.RuleFor(item => item.Network).Must(network => SiteSocialLink.Networks.Contains(network)).WithMessage("Unknown social network.");
            link.RuleFor(item => item.Url).NotEmpty().MaximumLength(300).Must(BeHttpsUrl).WithMessage("Social links must be full https:// addresses.");
        });
    }

    private static Func<IReadOnlyDictionary<string, string>, bool> BeLocalized(int maxLength) =>
        value => value is null || value.All(pair => Languages.Contains(pair.Key) && (pair.Value ?? string.Empty).Length <= maxLength);

    private static bool BeHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host) && uri.UserInfo.Length == 0;
}
