namespace Onyx.Contracts.Customers;

public sealed record AiDemoRunResponse(Guid Id, string ProductName, string Status, string Prompt, string? Output, string? FailureCode, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc);
