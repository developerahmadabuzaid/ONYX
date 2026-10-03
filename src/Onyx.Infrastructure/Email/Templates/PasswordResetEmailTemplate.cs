namespace Onyx.Infrastructure.Email.Templates;

/// <summary>Admin password reset. Model: <c>name</c>, <c>link</c>, <c>minutes</c>. Copy pending owner review.</summary>
public sealed class PasswordResetEmailTemplate : IEmailTemplate
{
    public const string TemplateKey = "identity.password-reset";

    public string Key => TemplateKey;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["name", "link", "minutes"];

    public EmailContent Compose(string languageCode, IReadOnlyDictionary<string, string> model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var link = new Uri(model["link"], UriKind.Absolute);

        return languageCode switch
        {
            "ar" => new EmailContent(
                Subject: "إعادة تعيين كلمة المرور",
                Heading: $"مرحباً {model["name"]}",
                Paragraphs:
                [
                    "تلقّينا طلباً لإعادة تعيين كلمة المرور لحسابك في لوحة إدارة Onyx.",
                    $"اضغط على الزر أدناه لاختيار كلمة مرور جديدة. تنتهي صلاحية هذا الرابط خلال {model["minutes"]} دقيقة.",
                ],
                Action: new EmailAction("إعادة تعيين كلمة المرور", link),
                Footer: "إذا لم تطلب ذلك، تجاهل هذه الرسالة وستبقى كلمة المرور الحالية كما هي."),
            "en" => new EmailContent(
                Subject: "Reset your password",
                Heading: $"Hello {model["name"]}",
                Paragraphs:
                [
                    "We received a request to reset the password of your Onyx admin account.",
                    $"Use the button below to choose a new password. This link expires in {model["minutes"]} minutes.",
                ],
                Action: new EmailAction("Reset password", link),
                Footer: "If you didn't request this, ignore this email and your current password will stay the same."),
            _ => throw new ArgumentOutOfRangeException(nameof(languageCode), languageCode, "Unsupported language."),
        };
    }
}
