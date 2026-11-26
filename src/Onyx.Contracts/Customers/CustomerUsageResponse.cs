namespace Onyx.Contracts.Customers;

public sealed record CustomerUsageResponse(bool AiDemoAvailable, int AiDemoRunsToday, int AiDemoDailyLimit, int AiDemoRunsThisMonth);
