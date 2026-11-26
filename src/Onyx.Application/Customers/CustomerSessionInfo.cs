namespace Onyx.Application.Customers;

/// <summary>A validated customer session (the result of checking an opaque session token).</summary>
public sealed record CustomerSessionInfo(Guid CustomerId, Guid SessionId);
