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

internal sealed class KeywordTargetRepository(ApplicationDbContext dbContext) : IKeywordTargetRepository
{
    public Task<KeywordTarget?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.KeywordTargets.SingleOrDefaultAsync(target => target.Id == id, cancellationToken);

    public async Task<PagedResult<KeywordTarget>> ListAsync(
        string? languageCode, KeywordStatus? status, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.KeywordTargets.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            query = query.Where(target => target.LanguageCode == languageCode);
        }

        if (status is { } filter)
        {
            query = query.Where(target => target.Status == filter);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());
            query = query.Where(target => EF.Functions.ILike(target.Keyword, pattern, LikePattern.EscapeCharacter));
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(target => target.LanguageCode).ThenBy(target => target.NormalizedKeyword)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<KeywordTarget>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<KeywordTarget>> FindAsync(string languageCode, string normalizedKeyword, CancellationToken cancellationToken) =>
        await dbContext.KeywordTargets.AsNoTracking().Where(target => target.LanguageCode == languageCode && target.NormalizedKeyword == normalizedKeyword)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<bool> ExistsAsync(string languageCode, string normalizedKeyword, string targetMarket, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.KeywordTargets.AnyAsync(target => target.LanguageCode == languageCode && target.NormalizedKeyword == normalizedKeyword
            && target.TargetMarket == targetMarket && target.Id != excludeId, cancellationToken);

    public void Add(KeywordTarget target) => dbContext.KeywordTargets.Add(target);

    public void Remove(KeywordTarget target) => dbContext.KeywordTargets.Remove(target);

    public void SetExpectedVersion(KeywordTarget target, uint version) => dbContext.Entry(target).Property(entry => entry.Version).OriginalValue = version;
}
