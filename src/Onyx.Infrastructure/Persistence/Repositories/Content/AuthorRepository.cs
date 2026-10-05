using Microsoft.EntityFrameworkCore;
using Onyx.Application.Insights;
using Onyx.Domain.Insights;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class AuthorRepository(ApplicationDbContext dbContext) : IAuthorRepository
{
    public Task<Author?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Authors.SingleOrDefaultAsync(author => author.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Author>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Authors.AsNoTracking().OrderBy(author => author.CreatedAtUtc).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<bool> IsCreditedAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ContentItems.AnyAsync(item => item.AuthorId == id, cancellationToken);

    public void Add(Author author) => dbContext.Authors.Add(author);

    public void Remove(Author author) => dbContext.Authors.Remove(author);

    public void SetExpectedVersion(Author author, uint version) => dbContext.Entry(author).Property(entry => entry.Version).OriginalValue = version;
}
