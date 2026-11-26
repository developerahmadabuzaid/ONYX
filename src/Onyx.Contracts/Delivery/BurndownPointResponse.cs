namespace Onyx.Contracts.Delivery;

/// <summary>Remaining work at the end of <see cref="Day"/>.</summary>
public sealed record BurndownPointResponse(DateOnly Day, int RemainingPoints, int RemainingIssues);
