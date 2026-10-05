using Onyx.Application.Email;
using Onyx.Application.Leads;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>
/// Internal new-inquiry notification. Model: <c>type</c>, <c>reference</c>, <c>language</c>, optional <c>link</c> (the lead in Admin).
/// Deliberately carries no visitor contact details or message: staff open the lead in Admin, where permissions apply.
/// </summary>
public sealed class InquiryInternalEmailTemplate : IEditableEmailTemplate
{
    public string Key => LeadNotifier.InquiryInternalTemplate;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["type", "reference", "language"];

    public IReadOnlyCollection<string> Placeholders { get; } = ["type", "reference", "language"];

    public IReadOnlyCollection<string> RequiredPlaceholders { get; } = ["reference"];

    public bool HasAction => true;

    public EmailCopy DefaultCopy(string languageCode) => languageCode switch
    {
        "ar" => new EmailCopy(
            "طلب جديد: {type} ({reference})",
            "وصل طلب جديد",
            [
                "النوع: {type}",
                "رقم المرجع: {reference}",
                "لغة الإرسال: {language}",
                "افتح العميل المحتمل في لوحة الإدارة للاطلاع على التفاصيل.",
            ],
            "فتح العميل المحتمل",
            "بيانات التواصل متاحة في لوحة الإدارة فقط."),
        "en" => new EmailCopy(
            "New inquiry: {type} ({reference})",
            "A new inquiry has arrived",
            [
                "Type: {type}",
                "Reference: {reference}",
                "Submission language: {language}",
                "Open the lead in the admin dashboard to see the details.",
            ],
            "Open lead",
            "Contact details are available in the admin dashboard only."),
        _ => throw new ArgumentOutOfRangeException(nameof(languageCode), languageCode, "Unsupported language."),
    };

    public Uri? ActionUrl(IReadOnlyDictionary<string, string> model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.TryGetValue("link", out var link) ? new Uri(link, UriKind.Absolute) : null;
    }

    public EmailContent Compose(string languageCode, IReadOnlyDictionary<string, string> model) =>
        EditableEmail.Compose(this, DefaultCopy(languageCode), model);
}
