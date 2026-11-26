namespace Onyx.Contracts.Customers;

/// <summary>The opaque session token is returned only to Onyx.Web, which keeps it inside its encrypted cookie; it never reaches browser JavaScript.</summary>
public sealed record CustomerSessionResponse(string SessionToken, DateTimeOffset AbsoluteExpiresAtUtc, Guid CustomerId, string DisplayName, string Language);
