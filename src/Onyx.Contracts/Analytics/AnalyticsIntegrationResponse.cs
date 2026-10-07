namespace Onyx.Contracts.Analytics;

/// <summary>
/// An analytics provider's operational state for administrators. Holds no credentials, tokens, account identifiers or provider
/// messages: <see cref="LastError"/> is a safe code.
/// </summary>
public sealed record AnalyticsIntegrationResponse(
    string ProviderKey,
    string ConnectionStatus,
    DateTimeOffset? LastCheckedAtUtc,
    DateTimeOffset? LastSucceededAtUtc,
    string? LastError,
    AnalyticsDataStatusResponse Data,
    string SyncJob);
