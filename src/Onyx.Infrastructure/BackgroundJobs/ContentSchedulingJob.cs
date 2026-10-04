using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Onyx.Application.Content;
using Onyx.Application.Jobs;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>
/// Publishes approved content whose scheduled time has come and unpublishes content whose end time has passed. Each item runs
/// in its own scope and unit of work; a failed publication returns the item to Approved and notifies editors. Idempotent.
/// </summary>
public sealed partial class ContentSchedulingJob(IServiceScopeFactory scopeFactory, ILogger<ContentSchedulingJob> logger) : IScheduledJob
{
    public const string JobName = "content-scheduling";

    public string Name => JobName;

    public string DefaultSchedule => "every:1m";

    public async Task<JobOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> duePublish;
        IReadOnlyList<Guid> dueUnpublish;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var scheduling = scope.ServiceProvider.GetRequiredService<IContentSchedulingService>();
            duePublish = await scheduling.ListDuePublicationsAsync(cancellationToken).ConfigureAwait(false);
            dueUnpublish = await scheduling.ListDueUnpublicationsAsync(cancellationToken).ConfigureAwait(false);
        }

        if (duePublish.Count == 0 && dueUnpublish.Count == 0)
        {
            return JobOutcome.Skipped("Nothing is due.");
        }

        var published = 0;
        var failed = 0;
        foreach (var id in duePublish)
        {
            var result = await RunAsync(id, (service, itemId) => service.PublishDueAsync(itemId, cancellationToken)).ConfigureAwait(false);
            if (result is null)
            {
                published++;
                continue;
            }

            failed++;
            LogPublishFailed(logger, id, result);
            await using var recovery = scopeFactory.CreateAsyncScope();
            await recovery.ServiceProvider.GetRequiredService<IContentSchedulingService>().HandleFailedPublicationAsync(id, result, cancellationToken).ConfigureAwait(false);
        }

        var unpublished = 0;
        foreach (var id in dueUnpublish)
        {
            var result = await RunAsync(id, (service, itemId) => service.UnpublishDueAsync(itemId, cancellationToken)).ConfigureAwait(false);
            if (result is null)
            {
                unpublished++;
            }
            else
            {
                failed++;
                LogUnpublishFailed(logger, id, result);
            }
        }

        return JobOutcome.Succeeded($"Published {published}, unpublished {unpublished}, failed {failed}.");
    }

    /// <summary>Null on success; otherwise a short reason (the scope and its partial changes are discarded).</summary>
    private async Task<string?> RunAsync(Guid id, Func<IContentSchedulingService, Guid, Task<Domain.Common.Result>> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        try
        {
            var result = await action(scope.ServiceProvider.GetRequiredService<IContentSchedulingService>(), id).ConfigureAwait(false);
            return result.IsSuccess ? null : $"{result.Error!.Code}: {result.Error.Message}";
        }
        catch (Application.Abstractions.Persistence.ConcurrencyConflictException)
        {
            return null; // Someone changed the item meanwhile; the next run re-evaluates it.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled publication of content {ContentItemId} failed: {Reason}")]
    private static partial void LogPublishFailed(ILogger logger, Guid contentItemId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled unpublication of content {ContentItemId} failed: {Reason}")]
    private static partial void LogUnpublishFailed(ILogger logger, Guid contentItemId, string reason);
}
