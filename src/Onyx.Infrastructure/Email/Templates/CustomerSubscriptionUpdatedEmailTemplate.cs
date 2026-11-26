using Onyx.Application.Customers;
using Onyx.Application.Email;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>A subscription changed status (staff decision or trial end). Model: <c>name</c>, <c>product</c>, <c>status</c>. Default copy pending owner review; wording is admin-editable.</summary>
public sealed class CustomerSubscriptionUpdatedEmailTemplate : IEditableEmailTemplate
{
    public string Key => CustomerEmailTemplates.SubscriptionUpdated;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["name", "product", "status"];

    public IReadOnlyCollection<string> Placeholders { get; } = ["name", "product", "status"];

    public IReadOnlyCollection<string> RequiredPlaceholders { get; } = ["status"];

    public bool HasAction => false;

    public EmailCopy DefaultCopy(string languageCode) => languageCode switch
    {
        "ar" => new EmailCopy(
            "تحديث على اشتراكك في أونيكس",
            "مرحباً {name}",
            [
                "تغيّرت حالة اشتراكك في {product} إلى: {status}.",
                "يمكنك متابعة التفاصيل في صفحة اشتراكاتي داخل حسابك.",
            ],
            null,
            "لأي استفسار، يسعدنا تواصلك معنا."),
        "en" => new EmailCopy(
            "An update on your Onyx subscription",
            "Hello {name}",
            [
                "Your subscription to {product} is now: {status}.",
                "You can see the details under Subscriptions in your account.",
            ],
            null,
            "If you have any questions, we're happy to help."),
        _ => throw new ArgumentOutOfRangeException(nameof(languageCode), languageCode, "Unsupported language."),
    };

    public Uri? ActionUrl(IReadOnlyDictionary<string, string> model) => null;

    /// <summary>The stored status is an enum name; it is shown in the customer's language.</summary>
    public EmailContent Compose(string languageCode, IReadOnlyDictionary<string, string> model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var localized = new Dictionary<string, string>(model, StringComparer.Ordinal) { ["status"] = StatusText(languageCode, model["status"]) };
        return EditableEmail.Compose(this, DefaultCopy(languageCode), localized);
    }

    private static string StatusText(string languageCode, string status) => (languageCode, status) switch
    {
        ("ar", "Active") => "مفعّل",
        ("ar", "Rejected") => "لم تتم الموافقة عليه",
        ("ar", "Cancelled") => "ملغى",
        ("ar", "Expired") => "انتهت الفترة التجريبية",
        ("ar", "Trial") => "فترة تجريبية",
        ("ar", _) => "قيد المراجعة",
        (_, "Active") => "active",
        (_, "Rejected") => "not approved",
        (_, "Cancelled") => "cancelled",
        (_, "Expired") => "trial ended",
        (_, "Trial") => "on trial",
        _ => "under review",
    };
}
