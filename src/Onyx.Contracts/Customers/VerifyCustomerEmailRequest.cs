namespace Onyx.Contracts.Customers;

public sealed record VerifyCustomerEmailRequest(Guid CustomerId, string Token);
