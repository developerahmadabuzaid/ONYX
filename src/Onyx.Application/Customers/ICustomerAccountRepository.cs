using Onyx.Domain.Customers;

namespace Onyx.Application.Customers;

public interface ICustomerAccountRepository
{
    Task<CustomerAccount?> GetAsync(Guid id, CancellationToken cancellationToken);

    void SetExpectedVersion(CustomerAccount account, uint version);
}
