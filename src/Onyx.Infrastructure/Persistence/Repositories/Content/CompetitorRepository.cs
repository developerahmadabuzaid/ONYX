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

internal sealed class CompetitorRepository(ApplicationDbContext dbContext) : ICompetitorRepository
{
    public Task<CompetitorReference?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.CompetitorReferences.SingleOrDefaultAsync(reference => reference.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CompetitorReference>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.CompetitorReferences.AsNoTracking().OrderBy(reference => reference.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<bool> DomainExistsAsync(string domain, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.CompetitorReferences.AnyAsync(reference => reference.Domain == domain && reference.Id != excludeId, cancellationToken);

    public void Add(CompetitorReference reference) => dbContext.CompetitorReferences.Add(reference);

    public void Remove(CompetitorReference reference) => dbContext.CompetitorReferences.Remove(reference);

    public void SetExpectedVersion(CompetitorReference reference, uint version) => dbContext.Entry(reference).Property(entry => entry.Version).OriginalValue = version;
}
