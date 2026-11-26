namespace Onyx.Contracts.Commerce;

public sealed record AdminCustomerListItemResponse(Guid Id, string Email, string DisplayName, string? Company, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset? LastSignInAtUtc, int OpenSubscriptions);
