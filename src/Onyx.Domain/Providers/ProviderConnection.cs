using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Providers;

/// <summary>
/// Operational status of an external provider (email transport, GA4, Search Console, LLM…). Holds no secrets:
/// credentials live only in the secret store. Never shows data as available unless the provider is actually working.
/// </summary>
public sealed class ProviderConnection : AuditableEntity, IAuditable
{
    public const int ProviderKeyMaxLength = 100;
    public const int LastErrorMaxLength = 500;

    private ProviderConnection()
    {
        ProviderKey = string.Empty;
    }

    public string ProviderKey { get; private set; }

    public ProviderCategory Category { get; private set; }

    public ProviderConnectionStatus Status { get; private set; }

    public DateTimeOffset? LastCheckedAtUtc { get; private set; }

    public DateTimeOffset? LastSucceededAtUtc { get; private set; }

    /// <summary>Short, credential-free description of the last failure.</summary>
    public string? LastError { get; private set; }

    /// <summary>PostgreSQL <c>xmin</c> optimistic concurrency token.</summary>
    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Integrations;

    public bool IsUsable => Status is ProviderConnectionStatus.Configured or ProviderConnectionStatus.Active or ProviderConnectionStatus.Error;

    public static ProviderConnection Register(string providerKey, ProviderCategory category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        if (providerKey.Length > ProviderKeyMaxLength)
        {
            throw new ArgumentException("Provider key is too long.", nameof(providerKey));
        }

        return new ProviderConnection { ProviderKey = providerKey, Category = category, Status = ProviderConnectionStatus.NotConfigured };
    }

    /// <summary>Credentials are missing or were removed.</summary>
    public void MarkNotConfigured()
    {
        if (Status != ProviderConnectionStatus.Disabled)
        {
            Status = ProviderConnectionStatus.NotConfigured;
        }
    }

    /// <summary>Credentials exist but have not been proven to work yet.</summary>
    public void MarkConfigured()
    {
        if (Status == ProviderConnectionStatus.NotConfigured)
        {
            Status = ProviderConnectionStatus.Configured;
        }
    }

    public void RecordSuccess(DateTimeOffset checkedAtUtc)
    {
        if (Status == ProviderConnectionStatus.Disabled)
        {
            return;
        }

        Status = ProviderConnectionStatus.Active;
        LastCheckedAtUtc = checkedAtUtc;
        LastSucceededAtUtc = checkedAtUtc;
        LastError = null;
    }

    public void RecordFailure(DateTimeOffset checkedAtUtc, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        if (Status == ProviderConnectionStatus.Disabled)
        {
            return;
        }

        var wasFailing = Status == ProviderConnectionStatus.Error;
        Status = ProviderConnectionStatus.Error;
        LastCheckedAtUtc = checkedAtUtc;
        LastError = error.Length > LastErrorMaxLength ? error[..LastErrorMaxLength] : error;

        if (!wasFailing)
        {
            RaiseDomainEvent(new ProviderConnectionFailed(Id, ProviderKey));
        }
    }

    public void Disable() => Status = ProviderConnectionStatus.Disabled;

    public void Enable()
    {
        if (Status == ProviderConnectionStatus.Disabled)
        {
            Status = ProviderConnectionStatus.NotConfigured;
        }
    }
}
