using Onyx.Application.Customers;
using Onyx.Application.Email;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>Customer password reset. Model: <c>name</c>, <c>link</c>, <c>minutes</c>. Default copy pending owner review; wording is admin-editable.</summary>
public sealed class CustomerPasswordResetEmailTemplate : IEditableEmailTemplate
{
    public string Key => CustomerEmailTemplates.PasswordReset;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["name", "link", "minutes"];

    public IReadOnlyCollection<string> Placeholders { get; } = ["name", "minutes"];

    public IReadOnlyCollection<string> RequiredPlaceholders { get; } = ["minutes"];

    public bool HasAction => true;

    public EmailCopy DefaultCopy(string languageCode) => languageCode switch
    {
        "ar" => new EmailCopy(
            "إعادة تعيين كلمة مرور حسابك في أونيكس",
            "مرحباً {name}",
            [
                "تلقّينا طلباً لإعادة تعيين كلمة مرور حسابك.",
                "اضغط على الزر أدناه لاختيار كلمة مرور جديدة. تنتهي صلاحية الرابط خلال {minutes} دقيقة.",
            ],
            "إعادة تعيين كلمة المرور",
            "إذا لم تطلب ذلك، تجاهل هذه الرسالة وستبقى كلمة المرور كما هي."),
        "en" => new EmailCopy(
            "Reset your Onyx account password",
            "Hello {name}",
            [
                "We received a request to reset your account password.",
                "Use the button below to choose a new password. This link expires in {minutes} minutes.",
            ],
            "Reset password",
            "If you didn't request this, ignore this email and your password will stay the same."),
        _ => throw new ArgumentOutOfRangeException(nameof(languageCode), languageCode, "Unsupported language."),
    };

    public Uri? ActionUrl(IReadOnlyDictionary<string, string> model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new Uri(model["link"], UriKind.Absolute);
    }

    public EmailContent Compose(string languageCode, IReadOnlyDictionary<string, string> model) =>
        EditableEmail.Compose(this, DefaultCopy(languageCode), model);
}
