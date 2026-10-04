using Onyx.Application.Email;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>Admin account invitation. Model: <c>name</c>, <c>link</c>, <c>hours</c>. Default copy pending owner review; wording is admin-editable.</summary>
public sealed class InvitationEmailTemplate : IEditableEmailTemplate
{
    public const string TemplateKey = "identity.invitation";

    public string Key => TemplateKey;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["name", "link", "hours"];

    public IReadOnlyCollection<string> Placeholders { get; } = ["name", "hours"];

    public IReadOnlyCollection<string> RequiredPlaceholders { get; } = ["hours"];

    public bool HasAction => true;

    public EmailCopy DefaultCopy(string languageCode) => languageCode switch
    {
        "ar" => new EmailCopy(
            "دعوة للانضمام إلى لوحة إدارة Onyx",
            "مرحباً {name}",
            [
                "تمت دعوتك لإنشاء حساب في لوحة إدارة Onyx Vanguard Solutions.",
                "اضغط على الزر أدناه لتعيين كلمة المرور وتفعيل حسابك. تنتهي صلاحية هذا الرابط خلال {hours} ساعة.",
            ],
            "تعيين كلمة المرور",
            "إذا لم تكن تتوقع هذه الدعوة، يمكنك تجاهل هذه الرسالة."),
        "en" => new EmailCopy(
            "Invitation to the Onyx admin dashboard",
            "Hello {name}",
            [
                "You have been invited to create an account on the Onyx Vanguard Solutions admin dashboard.",
                "Use the button below to set your password and activate your account. This link expires in {hours} hours.",
            ],
            "Set password",
            "If you weren't expecting this invitation, you can ignore this email."),
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
