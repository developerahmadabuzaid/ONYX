using Onyx.Domain.Common;

namespace Onyx.Domain.Analytics;

/// <summary>
/// What a provider's sync has actually loaded: the covered date range, when it last ran, succeeded or failed, and whether the
/// last run hit a size cap. Together with the provider connection status this tells dashboards the difference between
/// "not configured", "connected, no data", "failed (showing data up to …)" and "stale". Failure codes are safe identifiers only.
/// </summary>
public sealed class AnalyticsSyncState : Entity
{
    public const int ProviderKeyMaxLength = 100;
    public const int FailureCodeMaxLength = 100;

    private AnalyticsSyncState()
    {
        ProviderKey = string.Empty;
    }

    public string ProviderKey { get; private set; }

    public DateTimeOffset? LastAttemptAtUtc { get; private set; }

    public DateTimeOffset? LastSuccessAtUtc { get; private set; }

    public DateTimeOffset? LastFailureAtUtc { get; private set; }

    public string? LastFailureCode { get; private set; }

    /// <summary>Earliest day held locally.</summary>
    public DateOnly? DataFromDate { get; private set; }

    /// <summary>Latest day synced (the provider's data for later days is not final yet).</summary>
    public DateOnly? DataThroughDate { get; private set; }

    public int LastRowCount { get; private set; }

    /// <summary>The last run reached a row cap; the smallest rows of some days are summed into "(other)".</summary>
    public bool LastRunTruncated { get; private set; }

    public uint Version { get; private set; }

    /// <summary>A run started after the last success or failure and has not finished.</summary>
    public bool IsSyncing => LastAttemptAtUtc is { } attempt && attempt > (LastSuccessAtUtc ?? DateTimeOffset.MinValue) && attempt > (LastFailureAtUtc ?? DateTimeOffset.MinValue);

    public static AnalyticsSyncState For(string providerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        return new AnalyticsSyncState { ProviderKey = providerKey.Length > ProviderKeyMaxLength ? providerKey[..ProviderKeyMaxLength] : providerKey };
    }

    public void Start(DateTimeOffset now) => LastAttemptAtUtc = now;

    public void Succeed(DateTimeOffset now, DateOnly from, DateOnly through, int rows, bool truncated)
    {
        LastSuccessAtUtc = now;
        LastFailureCode = null;
        DataFromDate = DataFromDate is { } earliest && earliest < from ? earliest : from;
        DataThroughDate = DataThroughDate is { } latest && latest > through ? latest : through;
        LastRowCount = Math.Max(rows, 0);
        LastRunTruncated = truncated;
    }

    public void Fail(DateTimeOffset now, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        LastFailureAtUtc = now;
        LastFailureCode = code.Length > FailureCodeMaxLength ? code[..FailureCodeMaxLength] : code;
    }
}
