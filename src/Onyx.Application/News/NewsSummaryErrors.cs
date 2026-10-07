namespace Onyx.Application.News;

public static class NewsSummaryErrors
{
    /// <summary>No summary service is connected (the default until the external Agent is integrated).</summary>
    public const string NotConfigured = "NewsSummary.NotConfigured";

    /// <summary>The service did not answer within the time limit.</summary>
    public const string Timeout = "NewsSummary.Timeout";

    /// <summary>The service failed (transport error, error response).</summary>
    public const string Failed = "NewsSummary.Failed";
}
