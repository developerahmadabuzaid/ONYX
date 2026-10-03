using Onyx.Domain.Providers;

namespace Onyx.Application.Providers;

/// <summary>Records provider health as observed by the code that uses the provider (jobs, senders, connection tests).</summary>
public interface IProviderConnectionService
{
    Task ReportNotConfiguredAsync(string providerKey, ProviderCategory category, CancellationToken cancellationToken);

    Task ReportSuccessAsync(string providerKey, ProviderCategory category, CancellationToken cancellationToken);

    /// <param name="error">Short, credential-free description; never an exception message that might contain secrets.</param>
    Task ReportFailureAsync(string providerKey, ProviderCategory category, string error, CancellationToken cancellationToken);
}
