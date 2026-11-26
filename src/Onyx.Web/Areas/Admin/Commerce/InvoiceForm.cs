namespace Onyx.Web.Areas.Admin.Commerce;

/// <summary>An invoice draft. Lines are entered one per row as: description | quantity | unit price.</summary>
public sealed class InvoiceForm
{
    public Guid CustomerId { get; set; }

    public Guid? SubscriptionId { get; set; }

    public string? Currency { get; set; }

    public DateOnly DueOn { get; set; }

    public string? Notes { get; set; }

    public string? Lines { get; set; }

    public uint? Version { get; set; }
}
