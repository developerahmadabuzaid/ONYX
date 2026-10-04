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

internal sealed class ContentRelationRepository(ApplicationDbContext dbContext) : IContentRelationRepository
{
    public async Task<IReadOnlyList<ContentRelation>> ListFromAsync(Guid fromContentItemId, CancellationToken cancellationToken) =>
        await dbContext.ContentRelations.AsNoTracking().Where(relation => relation.FromContentItemId == fromContentItemId)
            .OrderBy(relation => relation.Position).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<ContentRelation?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ContentRelations.SingleOrDefaultAsync(relation => relation.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid fromId, Guid toId, RelationKind kind, CancellationToken cancellationToken) =>
        dbContext.ContentRelations.AnyAsync(relation => relation.FromContentItemId == fromId && relation.ToContentItemId == toId && relation.Kind == kind, cancellationToken);

    public void Add(ContentRelation relation) => dbContext.ContentRelations.Add(relation);

    public void Remove(ContentRelation relation) => dbContext.ContentRelations.Remove(relation);
}
