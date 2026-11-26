namespace Onyx.Contracts.Customers;

/// <summary>
/// Localized first URL segments of the customer pages, shared by Onyx.Web (routing) and the API (links in emails). Changing a
/// value breaks links already sent by email. All customer pages are private (noindex).
/// </summary>
public static class CustomerRouteSegments
{
    public static IReadOnlyDictionary<string, string> SignIn { get; } = Pair("تسجيل-الدخول", "sign-in");

    public static IReadOnlyDictionary<string, string> Register { get; } = Pair("إنشاء-حساب", "create-account");

    public static IReadOnlyDictionary<string, string> VerifyEmail { get; } = Pair("تأكيد-البريد", "verify-email");

    public static IReadOnlyDictionary<string, string> ForgotPassword { get; } = Pair("نسيت-كلمة-المرور", "forgot-password");

    public static IReadOnlyDictionary<string, string> ResetPassword { get; } = Pair("إعادة-تعيين-كلمة-المرور", "reset-password");

    public static IReadOnlyDictionary<string, string> SignOut { get; } = Pair("تسجيل-الخروج", "sign-out");

    public static IReadOnlyDictionary<string, string> Account { get; } = Pair("حسابي", "account");

    public static IReadOnlyDictionary<string, string> Profile { get; } = Pair("الملف-الشخصي", "profile");

    public static IReadOnlyDictionary<string, string> Subscriptions { get; } = Pair("اشتراكاتي", "subscriptions");

    public static IReadOnlyDictionary<string, string> Subscribe { get; } = Pair("اشترك", "subscribe");

    public static IReadOnlyDictionary<string, string> Invoices { get; } = Pair("فواتيري", "invoices");

    public static IReadOnlyDictionary<string, string> AiDemo { get; } = Pair("تجربة-الذكاء-الاصطناعي", "ai-demo");

    public static IEnumerable<string> All =>
        new[] { SignIn, Register, VerifyEmail, ForgotPassword, ResetPassword, SignOut, Account, Profile, Subscriptions, Subscribe, Invoices, AiDemo }
            .SelectMany(segments => segments.Values);

    private static Dictionary<string, string> Pair(string arabic, string english) =>
        new(StringComparer.Ordinal) { ["ar"] = arabic, ["en"] = english };
}
