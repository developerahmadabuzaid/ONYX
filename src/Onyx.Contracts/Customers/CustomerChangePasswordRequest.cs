namespace Onyx.Contracts.Customers;

public sealed record CustomerChangePasswordRequest(string CurrentPassword, string NewPassword);
