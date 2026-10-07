using Microsoft.EntityFrameworkCore;
using Onyx.Application.Brand;
using Onyx.Application.Content;
using Onyx.Application.Email;
using Onyx.Application.Intelligence;
using Onyx.Application.Knowledge;
using Onyx.Application.Media;
using Onyx.Contracts.Common;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class ContentPerformanceRepository(ApplicationDbContext dbContext) : IContentPerformanceRepository
{
    public async Task<IReadOnlyList<ContentPerformanceSnapshot>> ListAsync(Guid contentItemId, string languageCode, int take, CancellationToken cancellationToken) =>
        await dbContext.ContentPerformanceSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ContentItemId == contentItemId && snapshot.LanguageCode == languageCode)
            .OrderByDescending(snapshot => snapshot.PeriodEndUtc).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<bool> AnyAsync(CancellationToken cancellationToken) => dbContext.ContentPerformanceSnapshots.AnyAsync(cancellationToken);

    public async Task ReplacePeriodAsync(
        EvidenceSource source, DateTimeOffset periodStartUtc, IReadOnlyList<ContentPerformanceSnapshot> snapshots, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        await dbContext.ContentPerformanceSnapshots
            .Where(snapshot => snapshot.Source == source && snapshot.PeriodStartUtc == periodStartUtc)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        dbContext.ContentPerformanceSnapshots.AddRange(snapshots);
    }
}
