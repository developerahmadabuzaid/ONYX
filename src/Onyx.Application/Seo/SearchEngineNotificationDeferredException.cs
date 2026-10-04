namespace Onyx.Application.Seo;

/// <summary>A transient refusal (429 or 5xx). Thrown so the outbox retries the notification with backoff.</summary>
public sealed class SearchEngineNotificationDeferredException : Exception
{
    public SearchEngineNotificationDeferredException()
    {
    }

    public SearchEngineNotificationDeferredException(string message)
        : base(message)
    {
    }

    public SearchEngineNotificationDeferredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
