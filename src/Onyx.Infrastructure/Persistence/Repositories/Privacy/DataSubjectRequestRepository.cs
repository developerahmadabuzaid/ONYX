using Microsoft.EntityFrameworkCore;
using Onyx.Application.Privacy;
using Onyx.Contracts.Common;
using Onyx.Domain.Privacy;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class DataSubjectRequestRepository(ApplicationDbContext dbContext) : IDataSubjectRequestRepository
{
    public Task<DataSubjectRequest?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.DataSubjectRequests.SingleOrDefaultAsync(request => request.Id == id, cancellationToken);

    public async Task<DataSubjectRequest?> LockAsync(Guid id, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Locking a data-subject request requires an open transaction.");
        }

        // FOR UPDATE: a second execution of the same request waits here until the first commits, then sees its outcome.
        var locked = await dbContext.DataSubjectRequests
            .FromSql($"SELECT *, xmin FROM data_subject_requests WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return locked.SingleOrDefault();
    }

    public async Task<IReadOnlyList<DataSubjectRequest>> ListForSubjectAsync(string subjectEmailHash, CancellationToken cancellationToken) =>
        await dbContext.DataSubjectRequests.Where(request => request.SubjectEmailHash == subjectEmailHash).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<PagedResult<DataSubjectRequest>> ListAsync(DataSubjectRequestStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.DataSubjectRequests.AsNoTracking();
        if (status is { } wanted)
        {
            query = query.Where(request => request.Status == wanted);
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(request => request.ReceivedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<DataSubjectRequest>(items, page, pageSize, total);
    }

    public void Add(DataSubjectRequest request) => dbContext.DataSubjectRequests.Add(request);

    public void SetExpectedVersion(DataSubjectRequest request, uint version) =>
        dbContext.Entry(request).Property(entry => entry.Version).OriginalValue = version;
}
