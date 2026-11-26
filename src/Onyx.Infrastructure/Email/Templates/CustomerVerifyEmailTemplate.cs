using Onyx.Application.Customers;
using Onyx.Application.Email;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>Customer sign-up: confirm the email address. Model: <c>name</c>, <c>link</c>, <c>hours</c>. Default copy pending owner review; wording is admin-editable.</summary>
public sealed class CustomerVerifyEmailTemplate : IEditableEmailTemplate
{
    public string Key => CustomerEmailTemplates.VerifyEmail;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["name", "link", "hours"];

    public IReadOnlyCollection<string> Placeholders { get; } = ["name", "hours"];

    public IReadOnlyCollection<string> RequiredPlaceholders { get; } = ["hours"];

    public bool HasAction => true;

    public EmailCopy DefaultCopy(string languageCode) => languageCode switch
    {
        "ar" => new EmailCopy(
            "أكّد بريدك الإلكتروني لحسابك في أونيكس",
            "مرحباً {name}",
            [
                "شكراً لإنشاء حسابك في أونيكس.",
                "اضغط على الزر أدناه لتأكيد بريدك الإلكتروني وتفعيل الحساب. تنتهي صلاحية الرابط خلال {hours} ساعة.",
            ],
            "تأكيد البريد الإلكتروني",
            "إذا لم تنشئ حساباً لدينا، تجاهل هذه الرسالة ولن يُفعَّل أي حساب."),
        "en" => new EmailCopy(
            "Confirm your email for your Onyx account",
            "Hello {name}",
            [
                "Thank you for creating an Onyx account.",
                "Use the button below to confirm your email address and activate your account. This link expires in {hours} hours.",
            ],
            "Confirm email",
            "If you didn't create an account, ignore this email and no account will be activated."),
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
