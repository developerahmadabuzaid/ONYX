namespace Onyx.Contracts.Commerce;

public sealed record InvoiceLineResponse(string Description, decimal Quantity, decimal UnitPrice, decimal Amount);
