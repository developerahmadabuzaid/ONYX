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

internal sealed class InformationRequestRepository(ApplicationDbContext dbContext) : IInformationRequestRepository
{
    public Task<InformationRequest?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.InformationRequests.SingleOrDefaultAsync(request => request.Id == id, cancellationToken);

    public async Task<IReadOnlyList<InformationRequest>> ListAsync(InformationRequestStatus? status, CancellationToken cancellationToken)
    {
        var query = dbContext.InformationRequests.AsNoTracking();
        if (status is { } filter)
        {
            query = query.Where(request => request.Status == filter);
        }

        return await query.OrderByDescending(request => request.CreatedAtUtc).Take(500).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Add(InformationRequest request) => dbContext.InformationRequests.Add(request);
}
