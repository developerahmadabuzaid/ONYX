namespace Onyx.Contracts.Delivery;

/// <summary>Logs time spent on an issue.</summary>
public sealed record SaveWorkLogRequest(int Minutes, DateOnly WorkDate, string? Note);
