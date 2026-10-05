namespace Onyx.Application.Privacy;

/// <summary>One category of a retention run: counts only.</summary>
public sealed record RetentionRunSummary(string Category, string Action, int Processed, bool MoreRemaining);
