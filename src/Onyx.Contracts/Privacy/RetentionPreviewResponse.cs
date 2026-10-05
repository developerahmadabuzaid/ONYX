namespace Onyx.Contracts.Privacy;

/// <summary>What a policy with these settings would process now (a count only; nothing is changed).</summary>
public sealed record RetentionPreviewResponse(string Category, string Action, int PeriodDays, DateTimeOffset CutoffUtc, long EligibleCount);
