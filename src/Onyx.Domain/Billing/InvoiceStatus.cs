namespace Onyx.Domain.Billing;

/// <summary>Stored as text. Customers see Issued, Paid and Void invoices; drafts are staff-only.</summary>
public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    Paid = 2,
    Void = 3,
}
