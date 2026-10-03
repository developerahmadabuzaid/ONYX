namespace Onyx.Contracts.Administration;

/// <summary>Status only. Credentials are never stored in the database or returned by the API.</summary>
public sealed record ProviderConnectionResponse(
    string ProviderKey,
    string Category,
    string Status,
    DateTimeOffset? LastCheckedAtUtc,
    DateTimeOffset? LastSucceededAtUtc,
    string? LastError);
