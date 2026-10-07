namespace Onyx.Domain.News;

/// <summary>Stored as text. Why an ingested item was held for editors instead of being published automatically.</summary>
public enum NewsHoldReason
{
    /// <summary>No Onyx summary exists: the external summary service is not connected, failed, or timed out.</summary>
    SummaryUnavailable = 0,

    /// <summary>The service answered, but its summary failed the checks (length, languages, verbatim copying).</summary>
    SummaryRejected = 1,

    /// <summary>A summary exists, but the source is not set to publish automatically.</summary>
    AutoPublishOff = 2,

    /// <summary>The title matches a recent item from another feed entry; an editor decides.</summary>
    SimilarItem = 3,
}
