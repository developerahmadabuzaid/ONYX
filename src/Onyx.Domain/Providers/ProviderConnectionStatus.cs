namespace Onyx.Domain.Providers;

/// <summary>docs/architecture.md §15.1: NotConfigured → Configured → Active → Error / Disabled.</summary>
public enum ProviderConnectionStatus
{
    NotConfigured = 0,
    Configured = 1,
    Active = 2,
    Error = 3,
    Disabled = 4,
}
