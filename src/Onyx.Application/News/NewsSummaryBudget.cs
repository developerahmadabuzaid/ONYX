namespace Onyx.Application.News;

/// <summary>
/// Shared across the sources of one ingestion run: how many summary requests remain, and what the summary service did. Once
/// the service reports it is not configured, no further request is made in the run.
/// </summary>
public sealed class NewsSummaryBudget(int maxRequests)
{
    public int Remaining { get; private set; } = maxRequests;

    public bool NotConfigured { get; private set; }

    public string? LastFailureCode { get; private set; }

    public bool AnySucceeded { get; private set; }

    public int Requested { get; private set; }

    public bool TryTake()
    {
        if (NotConfigured || Remaining <= 0)
        {
            return false;
        }

        Remaining--;
        Requested++;
        return true;
    }

    public void RecordNotConfigured() => NotConfigured = true;

    public void RecordFailure(string code) => LastFailureCode = code;

    public void RecordSuccess() => AnySucceeded = true;
}
