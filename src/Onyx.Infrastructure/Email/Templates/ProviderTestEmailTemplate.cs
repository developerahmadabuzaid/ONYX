namespace Onyx.Infrastructure.Email.Templates;

/// <summary>Sent by the email provider connection test, so an administrator can confirm the transport really delivers.</summary>
public sealed class ProviderTestEmailTemplate : IEmailTemplate
{
    public const string TemplateKey = "system.provider-test";

    public string Key => TemplateKey;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = [];

    public EmailContent Compose(string languageCode, IReadOnlyDictionary<string, string> model) => languageCode switch
    {
        "ar" => new EmailContent(
            Subject: "رسالة اختبار من Onyx Vanguard Solutions",
            Heading: "إعدادات البريد الإلكتروني تعمل",
            Paragraphs:
            [
                "هذه رسالة آلية أُرسلت للتحقق من إعدادات البريد الإلكتروني في نظام Onyx.",
                "وصول هذه الرسالة يعني أن الإرسال يعمل بشكل صحيح، ولا يلزم اتخاذ أي إجراء.",
            ],
            Action: null,
            Footer: "رسالة آلية — يُرجى عدم الرد عليها."),
        "en" => new EmailContent(
            Subject: "Test message from Onyx Vanguard Solutions",
            Heading: "Email delivery is working",
            Paragraphs:
            [
                "This is an automated message sent to verify the email settings of the Onyx system.",
                "If you received it, delivery is working correctly and no action is needed.",
            ],
            Action: null,
            Footer: "Automated message — please do not reply."),
        _ => throw new ArgumentOutOfRangeException(nameof(languageCode), languageCode, "Unsupported language."),
    };
}
