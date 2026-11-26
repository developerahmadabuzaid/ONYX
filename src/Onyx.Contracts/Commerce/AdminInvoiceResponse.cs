namespace Onyx.Contracts.Commerce;

public sealed record AdminInvoiceResponse(Guid Id, string? Number, Guid CustomerId, string CustomerName, string CustomerEmail, Guid? SubscriptionId, string Status, string Currency, decimal Total, DateOnly DueOn, string? Notes, string? BillToName, string? BillToEmail, DateTimeOffset? IssuedAtUtc, DateTimeOffset? PaidAtUtc, DateTimeOffset? VoidedAtUtc, IReadOnlyList<InvoiceLineResponse> Lines, uint Version);
