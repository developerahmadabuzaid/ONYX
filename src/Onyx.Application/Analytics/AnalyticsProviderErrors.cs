namespace Onyx.Application.Analytics;

/// <summary>Safe failure codes of analytics providers (stored as the last failure; never provider messages or credentials).</summary>
public static class AnalyticsProviderErrors
{
    public const string NotConfigured = "Analytics.NotConfigured";

    /// <summary>Credentials were rejected or lack access to the property (expired key, removed permission).</summary>
    public const string Unauthorized = "Analytics.Unauthorized";

    public const string QuotaExceeded = "Analytics.QuotaExceeded";

    public const string Timeout = "Analytics.Timeout";

    /// <summary>The provider could not be reached or answered with an error.</summary>
    public const string Unavailable = "Analytics.Unavailable";

    /// <summary>The provider answered with data Onyx could not read.</summary>
    public const string InvalidResponse = "Analytics.InvalidResponse";
}
