namespace Onyx.Contracts.Customers;

public sealed record CustomerInvoiceResponse(Guid Id, string Number, string Status, string Currency, decimal Total, DateTimeOffset IssuedAtUtc, DateOnly DueOn, DateTimeOffset? PaidAtUtc, string? Notes, IReadOnlyList<Commerce.InvoiceLineResponse> Lines);
