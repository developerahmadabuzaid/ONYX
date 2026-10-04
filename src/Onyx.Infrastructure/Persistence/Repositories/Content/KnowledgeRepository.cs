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

internal sealed class KnowledgeRepository(ApplicationDbContext dbContext) : IKnowledgeRepository
{
    public Task<KnowledgeEntry?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.KnowledgeEntries.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);

    public async Task<IReadOnlyList<KnowledgeEntry>> ListAsync(KnowledgeCategory? category, KnowledgeStatus? status, string? search, CancellationToken cancellationToken)
    {
        var query = dbContext.KnowledgeEntries.AsNoTracking();
        if (category is { } categoryFilter)
        {
            query = query.Where(entry => entry.Category == categoryFilter);
        }

        if (status is { } statusFilter)
        {
            query = query.Where(entry => entry.Status == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());
            query = query.Where(entry => EF.Functions.ILike(entry.Title, pattern, LikePattern.EscapeCharacter));
        }

        return await query.OrderBy(entry => entry.Category).ThenBy(entry => entry.Title).Take(500).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Add(KnowledgeEntry entry) => dbContext.KnowledgeEntries.Add(entry);

    public void SetExpectedVersion(KnowledgeEntry entry, uint version) => dbContext.Entry(entry).Property(item => item.Version).OriginalValue = version;
}
