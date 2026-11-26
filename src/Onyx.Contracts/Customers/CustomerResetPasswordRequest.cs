namespace Onyx.Contracts.Customers;

public sealed record CustomerResetPasswordRequest(Guid CustomerId, string Token, string NewPassword);
