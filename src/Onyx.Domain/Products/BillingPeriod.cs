namespace Onyx.Domain.Products;

/// <summary>Stored as text.</summary>
public enum BillingPeriod
{
    Monthly = 0,
    Yearly = 1,
    OneTime = 2,

    /// <summary>Quoted per customer; never shown with a price.</summary>
    Custom = 3,
}
