namespace Onyx.Contracts.Delivery;

/// <summary>One timeline point: issues created and issues completed between <see cref="Start"/> and <see cref="End"/> (inclusive).</summary>
public sealed record DeliveryTrendPointResponse(DateOnly Start, DateOnly End, int Created, int Completed);
