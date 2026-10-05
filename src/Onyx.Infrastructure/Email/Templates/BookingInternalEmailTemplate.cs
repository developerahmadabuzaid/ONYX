using Onyx.Application.Booking;
using Onyx.Application.Email;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>
/// Internal booking notification (confirmed, rescheduled or cancelled). Model: <c>status</c>, <c>date</c>, <c>time</c>,
/// <c>timezone</c>, <c>minutes</c>, <c>reference</c>, <c>language</c>, optional <c>link</c>. No visitor contact details.
/// </summary>
public sealed class BookingInternalEmailTemplate : IEditableEmailTemplate
{
    public string Key => BookingMessages.InternalTemplate;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["status", "date", "time", "timezone", "minutes", "reference", "language"];

    public IReadOnlyCollection<string> Placeholders { get; } = ["status", "date", "time", "timezone", "minutes", "reference", "language"];

    public IReadOnlyCollection<string> RequiredPlaceholders { get; } = ["status", "date", "time"];

    public bool HasAction => true;

    public EmailCopy DefaultCopy(string languageCode) => languageCode switch
    {
        "ar" => new EmailCopy(
            "استشارة ({status}): {date} {time}",
            "تحديث على حجز استشارة",
            [
                "الحالة: {status}",
                "الموعد: {date} الساعة {time} بتوقيت {timezone} ({minutes} دقيقة)",
                "رقم المرجع: {reference} — لغة الزائر: {language}",
            ],
            "فتح العميل المحتمل",
            "بيانات التواصل متاحة في لوحة الإدارة فقط."),
        "en" => new EmailCopy(
            "Consultation ({status}): {date} {time}",
            "Consultation booking update",
            [
                "Status: {status}",
                "Time: {date} at {time} ({timezone}), {minutes} minutes",
                "Reference: {reference} — visitor language: {language}",
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
