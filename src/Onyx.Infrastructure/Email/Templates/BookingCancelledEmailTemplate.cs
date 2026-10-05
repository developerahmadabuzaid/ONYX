using Onyx.Application.Booking;
using Onyx.Application.Email;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>Visitor notice that a consultation was cancelled (with a cancelling calendar file). Model: <c>name</c>, <c>reference</c>, <c>date</c>, <c>time</c>, <c>timezone</c>.</summary>
public sealed class BookingCancelledEmailTemplate : IEditableEmailTemplate
{
    public string Key => BookingMessages.CancelledTemplate;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["name", "reference", "date", "time", "timezone"];

    public IReadOnlyCollection<string> Placeholders { get; } = ["name", "reference", "date", "time", "timezone"];

    public IReadOnlyCollection<string> RequiredPlaceholders { get; } = ["date", "time"];

    public bool HasAction => false;

    public EmailCopy DefaultCopy(string languageCode) => languageCode switch
    {
        "ar" => new EmailCopy(
            "تم إلغاء استشارتك: {date} {time}",
            "مرحباً {name}",
            [
                "تم إلغاء موعد الاستشارة بتاريخ {date} الساعة {time} بتوقيت {timezone} (رقم المرجع {reference}).",
                "يمكنك حجز موعد جديد في أي وقت من موقع Onyx Vanguard Solutions.",
            ],
            null,
            null),
        "en" => new EmailCopy(
            "Your consultation was cancelled: {date} {time}",
            "Hello {name}",
            [
                "The consultation on {date} at {time} ({timezone}) has been cancelled (reference {reference}).",
                "You can book a new time on the Onyx Vanguard Solutions website whenever suits you.",
            ],
            null,
            null),
        _ => throw new ArgumentOutOfRangeException(nameof(languageCode), languageCode, "Unsupported language."),
    };

    public Uri? ActionUrl(IReadOnlyDictionary<string, string> model) => null;

    public EmailContent Compose(string languageCode, IReadOnlyDictionary<string, string> model) =>
        EditableEmail.Compose(this, DefaultCopy(languageCode), model);
}
