using FluentValidation;
using Onyx.Application.Localization;

namespace Onyx.Application.Identity.Validation;

internal static class IdentityRules
{
    public const int PasswordMaxLength = 128;
    public const int DisplayNameMaxLength = 100;
    public const int TokenMaxLength = 2_048;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(254).EmailAddress();

    /// <summary>Length is enforced here; the full policy (minimum length, unique characters) is enforced by Identity.</summary>
    public static IRuleBuilderOptions<T, string> ValidNewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(PasswordMaxLength);

    public static IRuleBuilderOptions<T, string> ValidLanguage<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(SupportedLanguages.All.Contains).WithMessage("Unsupported language.");

    public static IRuleBuilderOptions<T, string> ValidDisplayName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(DisplayNameMaxLength)
            .Must(name => name.IndexOfAny(['\r', '\n', '<', '>']) < 0).WithMessage("Display name contains invalid characters.");
}
