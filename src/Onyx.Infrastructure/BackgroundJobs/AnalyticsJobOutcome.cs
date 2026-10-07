using Onyx.Application.Analytics;
using Onyx.Application.Jobs;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>Skipped stays skipped; a provider failure fails the job run (with the safe code only).</summary>
internal static class AnalyticsJobOutcome
{
    public static JobOutcome From(AnalyticsSyncOutcome outcome) => outcome.Status switch
    {
        "Skipped" => JobOutcome.Skipped(outcome.Message),
        "Failed" => throw new InvalidOperationException(outcome.Message),
        _ => JobOutcome.Succeeded(outcome.Message),
    };
}
