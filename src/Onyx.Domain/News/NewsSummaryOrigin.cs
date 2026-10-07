namespace Onyx.Domain.News;

/// <summary>Stored as text. Who wrote the Onyx summaries of an item.</summary>
public enum NewsSummaryOrigin
{
    /// <summary>Written or edited by an editor.</summary>
    Manual = 0,

    /// <summary>Produced by the connected external summary service and not edited since.</summary>
    Generated = 1,
}
