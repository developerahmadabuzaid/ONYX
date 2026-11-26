using Onyx.Domain.Subscriptions;

namespace Onyx.Application.Subscriptions;

public interface ICustomerSubscriptionRepository
{
    Task<CustomerSubscription?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> HasOpenAsync(Guid customerId, Guid productId, CancellationToken cancellationToken);

    Task<bool> HadTrialAsync(Guid customerId, Guid productId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerSubscription>> OpenForCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerSubscription>> TrialsEndedAsync(DateTimeOffset nowUtc, int max, CancellationToken cancellationToken);

    void Add(CustomerSubscription subscription);

    void SetExpectedVersion(CustomerSubscription subscription, uint version);
}
