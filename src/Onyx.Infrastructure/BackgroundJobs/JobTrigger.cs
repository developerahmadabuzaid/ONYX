using Microsoft.EntityFrameworkCore;
using Onyx.Application.Jobs;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>Makes a registered, enabled job due now; the Worker scheduler picks it up under its normal lease (never run here).</summary>
internal sealed class JobTrigger(ApplicationDbContext dbContext, TimeProvider timeProvider) : IJobTrigger
{
    public async Task<bool> RequestRunAsync(string jobName, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var updated = await dbContext.JobDefinitions
            .Where(job => job.Name == jobName && job.IsEnabled)
            .ExecuteUpdateAsync(setters => setters.SetProperty(job => job.NextRunAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
        return updated > 0;
    }
}
