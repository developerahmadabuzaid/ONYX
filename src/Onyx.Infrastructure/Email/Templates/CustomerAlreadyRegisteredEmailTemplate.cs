using Onyx.Application.Customers;
using Onyx.Application.Email;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>Someone tried to sign up with an address that already has an account. Model: <c>name</c>, <c>link</c>. Default copy pending owner review; wording is admin-editable.</summary>
public sealed class CustomerAlreadyRegisteredEmailTemplate : IEditableEmailTemplate
{
    public string Key => CustomerEmailTemplates.AlreadyRegistered;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["name", "link"];

    public IReadOnlyCollection<string> Placeholders { get; } = ["name"];

    public IReadOnlyCollection<string> RequiredPlaceholders { get; } = [];

    public bool HasAction => true;

    public EmailCopy DefaultCopy(string languageCode) => languageCode switch
    {
        "ar" => new EmailCopy(
            "لديك حساب في أونيكس بالفعل",
            "مرحباً {name}",
            [
                "طُلب للتو إنشاء حساب جديد بهذا البريد الإلكتروني، لكن لديك حساباً قائماً بالفعل.",
                "يمكنك تسجيل الدخول من الزر أدناه، أو استعادة كلمة المرور من صفحة تسجيل الدخول إن نسيتها.",
            ],
            "تسجيل الدخول",
            "إذا لم تكن أنت من طلب ذلك، فلا داعي لأي إجراء؛ حسابك لم يتغير."),
        "en" => new EmailCopy(
            "You already have an Onyx account",
            "Hello {name}",
            [
                "Someone just tried to create an account with this email address, but you already have one.",
                "You can sign in with the button below, or reset your password from the sign-in page if you've forgotten it.",
            ],
            "Sign in",
            "If this wasn't you, no action is needed; your account hasn't changed."),
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
