using Onyx.Application.Booking;
using Onyx.Application.Email;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>
/// Visitor booking confirmation with a calendar file attached. Model: <c>name</c>, <c>reference</c>, <c>date</c>, <c>time</c>,
/// <c>timezone</c>, <c>minutes</c>, optional <c>link</c> (the signed manage-booking page). The consultation format and location are
/// still open business decisions, so the copy does not state them.
/// </summary>
public sealed class BookingConfirmedEmailTemplate : IEditableEmailTemplate
{
    public string Key => BookingMessages.ConfirmedTemplate;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["name", "reference", "date", "time", "timezone", "minutes"];

    public IReadOnlyCollection<string> Placeholders { get; } = ["name", "reference", "date", "time", "timezone", "minutes"];

    public IReadOnlyCollection<string> RequiredPlaceholders { get; } = ["date", "time", "timezone"];

    public bool HasAction => true;

    public EmailCopy DefaultCopy(string languageCode) => languageCode switch
    {
        "ar" => new EmailCopy(
            "تم حجز استشارتك: {date} {time}",
            "شكراً لك، {name}",
            [
                "تم تأكيد موعد استشارتك مع Onyx Vanguard Solutions بتاريخ {date} الساعة {time} بتوقيت {timezone}.",
                "المدة: {minutes} دقيقة. رقم المرجع: {reference}.",
                "أرفقنا ملف تقويم يمكنك إضافته إلى تقويمك. لإلغاء الموعد أو تغييره استخدم زر إدارة الحجز.",
            ],
            "إدارة الحجز",
            "إذا لم تحجز هذه الاستشارة، يمكنك تجاهل هذه الرسالة."),
        "en" => new EmailCopy(
            "Your consultation is booked: {date} {time}",
            "Thank you, {name}",
            [
                "Your consultation with Onyx Vanguard Solutions is confirmed for {date} at {time} ({timezone}).",
                "Duration: {minutes} minutes. Reference: {reference}.",
                "A calendar file is attached. To cancel or change the time, use the manage booking button.",
            ],
            "Manage booking",
            "If you did not book this consultation, you can ignore this email."),
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
