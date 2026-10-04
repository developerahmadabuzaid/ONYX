using Onyx.Application.Email;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>Sent by the email provider connection test, so an administrator can confirm the transport really delivers.</summary>
public sealed class ProviderTestEmailTemplate : IEditableEmailTemplate
{
    public const string TemplateKey = "system.provider-test";

    public string Key => TemplateKey;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = [];

    public IReadOnlyCollection<string> Placeholders { get; } = [];

    public IReadOnlyCollection<string> RequiredPlaceholders { get; } = [];

    public bool HasAction => false;

    public EmailCopy DefaultCopy(string languageCode) => languageCode switch
    {
        "ar" => new EmailCopy(
            "رسالة اختبار من Onyx Vanguard Solutions",
            "إعدادات البريد الإلكتروني تعمل",
            [
                "هذه رسالة آلية أُرسلت للتحقق من إعدادات البريد الإلكتروني في نظام Onyx.",
                "وصول هذه الرسالة يعني أن الإرسال يعمل بشكل صحيح، ولا يلزم اتخاذ أي إجراء.",
            ],
            null,
            "رسالة آلية — يُرجى عدم الرد عليها."),
        "en" => new EmailCopy(
            "Test message from Onyx Vanguard Solutions",
            "Email delivery is working",
            [
                "This is an automated message sent to verify the email settings of the Onyx system.",
                "If you received it, delivery is working correctly and no action is needed.",
            ],
            null,
            "Automated message — please do not reply."),
        _ => throw new ArgumentOutOfRangeException(nameof(languageCode), languageCode, "Unsupported language."),
    };

    public Uri? ActionUrl(IReadOnlyDictionary<string, string> model) => null;

    public EmailContent Compose(string languageCode, IReadOnlyDictionary<string, string> model) =>
        EditableEmail.Compose(this, DefaultCopy(languageCode), model);
}
