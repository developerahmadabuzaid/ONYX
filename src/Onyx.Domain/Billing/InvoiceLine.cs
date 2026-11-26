using Onyx.Domain.Common;

namespace Onyx.Domain.Billing;

public sealed class InvoiceLine : Entity
{
    public const int DescriptionMaxLength = 200;
    public const decimal MaxQuantity = 100_000m;
    public const decimal MaxUnitPrice = 10_000_000m;

    private InvoiceLine()
    {
        Description = string.Empty;
    }

    public Guid InvoiceId { get; private set; }

    public int Position { get; private set; }

    public string Description { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal Amount { get; private set; }

    internal static Result<InvoiceLine> Create(Guid invoiceId, int position, string? description, decimal quantity, decimal unitPrice)
    {
        var clean = description?.Trim();
        if (string.IsNullOrEmpty(clean) || clean.Length > DescriptionMaxLength)
        {
            return Error.Validation("Invoice.InvalidLine", $"Each line needs a description (at most {DescriptionMaxLength} characters).");
        }

        if (quantity <= 0 || quantity > MaxQuantity || decimal.Round(quantity, 2) != quantity)
        {
            return Error.Validation("Invoice.InvalidQuantity", "Quantities are positive with at most two decimals.");
        }

        if (unitPrice < 0 || unitPrice > MaxUnitPrice || decimal.Round(unitPrice, 2) != unitPrice)
        {
            return Error.Validation("Invoice.InvalidUnitPrice", "Unit prices are zero or positive with at most two decimals.");
        }

        return new InvoiceLine
        {
            InvoiceId = invoiceId,
            Position = position,
            Description = clean,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Amount = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero),
        };
    }
}
