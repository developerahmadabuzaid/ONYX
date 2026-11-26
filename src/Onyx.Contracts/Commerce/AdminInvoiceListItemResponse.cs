namespace Onyx.Contracts.Commerce;

public sealed record AdminInvoiceListItemResponse(Guid Id, string? Number, Guid CustomerId, string CustomerName, string Status, string Currency, decimal Total, DateOnly DueOn, DateTimeOffset? IssuedAtUtc, DateTimeOffset? PaidAtUtc);
