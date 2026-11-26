namespace Onyx.Application.Customers;

/// <summary>The signed-in customer of the current API request (customer endpoints only); null everywhere else.</summary>
public interface ICurrentCustomer
{
    Guid? CustomerId { get; }

    Guid? SessionId { get; }
}
