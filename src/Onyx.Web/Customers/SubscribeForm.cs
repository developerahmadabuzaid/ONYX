namespace Onyx.Web.Customers;

/// <summary>Confirms a trial or subscription request.</summary>
public sealed class SubscribeForm
{
    public string? Product { get; set; }

    public string? Intent { get; set; }

    public Guid? PlanId { get; set; }

    public string? Note { get; set; }
}
