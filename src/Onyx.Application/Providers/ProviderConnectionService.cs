using Onyx.Application.Abstractions.Persistence;
using Onyx.Domain.Providers;

namespace Onyx.Application.Providers;

public sealed class ProviderConnectionService(IProviderConnectionRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : IProviderConnectionService
{
    public Task ReportNotConfiguredAsync(string providerKey, ProviderCategory category, CancellationToken cancellationToken) =>
        UpdateAsync(providerKey, category, connection => connection.MarkNotConfigured(), cancellationToken);

    public Task ReportSuccessAsync(string providerKey, ProviderCategory category, CancellationToken cancellationToken) =>
        UpdateAsync(providerKey, category, connection => connection.RecordSuccess(timeProvider.GetUtcNow()), cancellationToken);

    public Task ReportFailureAsync(string providerKey, ProviderCategory category, string error, CancellationToken cancellationToken) =>
        UpdateAsync(providerKey, category, connection => connection.RecordFailure(timeProvider.GetUtcNow(), error), cancellationToken);

    private async Task UpdateAsync(string providerKey, ProviderCategory category, Action<ProviderConnection> change, CancellationToken cancellationToken)
    {
        var connection = await repository.GetByKeyAsync(providerKey, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            connection = ProviderConnection.Register(providerKey, category);
            repository.Add(connection);
        }

        change(connection);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
