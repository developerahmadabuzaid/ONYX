namespace Onyx.Infrastructure.Email.Templates;

/// <summary>Admin account invitation. Model: <c>name</c>, <c>link</c>, <c>hours</c>. Copy pending owner review.</summary>
public sealed class InvitationEmailTemplate : IEmailTemplate
{
    public const string TemplateKey = "identity.invitation";

    public string Key => TemplateKey;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["name", "link", "hours"];

    public EmailContent Compose(string languageCode, IReadOnlyDictionary<string, string> model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var link = new Uri(model["link"], UriKind.Absolute);

        return languageCode switch
        {
            "ar" => new EmailContent(
                Subject: "دعوة للانضمام إلى لوحة إدارة Onyx",
                Heading: $"مرحباً {model["name"]}",
                Paragraphs:
                [
                    "تمت دعوتك لإنشاء حساب في لوحة إدارة Onyx Vanguard Solutions.",
                    $"اضغط على الزر أدناه لتعيين كلمة المرور وتفعيل حسابك. تنتهي صلاحية هذا الرابط خلال {model["hours"]} ساعة.",
                ],
                Action: new EmailAction("تعيين كلمة المرور", link),
                Footer: "إذا لم تكن تتوقع هذه الدعوة، يمكنك تجاهل هذه الرسالة."),
            "en" => new EmailContent(
                Subject: "Invitation to the Onyx admin dashboard",
                Heading: $"Hello {model["name"]}",
                Paragraphs:
                [
                    "You have been invited to create an account on the Onyx Vanguard Solutions admin dashboard.",
                    $"Use the button below to set your password and activate your account. This link expires in {model["hours"]} hours.",
                ],
                Action: new EmailAction("Set password", link),
                Footer: "If you weren't expecting this invitation, you can ignore this email."),
            _ => throw new ArgumentOutOfRangeException(nameof(languageCode), languageCode, "Unsupported language."),
        };
    }
}
