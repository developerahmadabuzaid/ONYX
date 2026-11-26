namespace Onyx.Contracts.Commerce;

public sealed record SaveInvoiceRequest(Guid CustomerId, Guid? SubscriptionId, string Currency, DateOnly DueOn, string? Notes, IReadOnlyList<InvoiceLineRequest> Lines, uint? Version);
