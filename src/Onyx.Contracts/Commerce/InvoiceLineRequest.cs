namespace Onyx.Contracts.Commerce;

public sealed record InvoiceLineRequest(string Description, decimal Quantity, decimal UnitPrice);
