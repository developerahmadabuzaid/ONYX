using Onyx.Application.Customers;

namespace Onyx.Infrastructure.Customers;

/// <summary>Hosts without customer requests (the Worker): there is never a signed-in customer. Onyx.API registers its HTTP version.</summary>
public sealed class NoCurrentCustomer : ICurrentCustomer
{
    public Guid? CustomerId => null;

    public Guid? SessionId => null;
}
