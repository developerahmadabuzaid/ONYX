using Onyx.Application.Email;
using Onyx.Application.Leads;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>Visitor confirmation of a form submission, in the submission language. Model: <c>name</c>, <c>reference</c>. Wording is admin-editable.</summary>
public sealed class InquiryReceivedEmailTemplate : IEditableEmailTemplate
{
    public string Key => LeadNotifier.InquiryReceivedTemplate;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["name", "reference"];

    public IReadOnlyCollection<string> Placeholders { get; } = ["name", "reference"];

    public IReadOnlyCollection<string> RequiredPlaceholders { get; } = ["reference"];

    public bool HasAction => false;

    public EmailCopy DefaultCopy(string languageCode) => languageCode switch
    {
        "ar" => new EmailCopy(
            "استلمنا طلبك ({reference})",
            "شكراً لك، {name}",
            [
                "استلمنا طلبك وسجّلناه برقم المرجع {reference}.",
                "سيراجع فريق Onyx طلبك ويتواصل معك عبر البريد الإلكتروني.",
            ],
            null,
            "وصلتك هذه الرسالة لأنك أرسلت طلباً عبر موقع Onyx Vanguard Solutions. إذا لم تكن أنت من أرسله، يمكنك تجاهلها."),
        "en" => new EmailCopy(
            "We received your request ({reference})",
            "Thank you, {name}",
            [
                "We have received your request and recorded it under reference {reference}.",
                "The Onyx team will review it and get back to you by email.",
            ],
            null,
            "You received this email because a request was submitted on the Onyx Vanguard Solutions website. If it wasn't you, you can ignore it."),
        _ => throw new ArgumentOutOfRangeException(nameof(languageCode), languageCode, "Unsupported language."),
    };

    public Uri? ActionUrl(IReadOnlyDictionary<string, string> model) => null;

    public EmailContent Compose(string languageCode, IReadOnlyDictionary<string, string> model) =>
        EditableEmail.Compose(this, DefaultCopy(languageCode), model);
}
