using Onyx.Application.Jobs;
using Onyx.Application.Subscriptions;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>Ends trials whose end date has passed and emails the customer. Idempotent: an expired trial is never touched again.</summary>
public sealed class TrialExpiryJob(SubscriptionAdministrationService subscriptions) : IScheduledJob
{
    public const string JobName = "trial-expiry";

    public string Name => JobName;

    public string DefaultSchedule => "every:15m";

    public async Task<JobOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        var expired = await subscriptions.ExpireEndedTrialsAsync(cancellationToken).ConfigureAwait(false);
        return expired == 0 ? JobOutcome.Skipped("No trial has ended.") : JobOutcome.Succeeded($"{expired} trial(s) expired.");
    }
}
