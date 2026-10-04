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

internal sealed class RedirectRepository(ApplicationDbContext dbContext) : IRedirectRepository
{
    public Task<Redirect?> FindActiveAsync(string languageCode, string fromPath, CancellationToken cancellationToken) =>
        dbContext.Redirects.AsNoTracking().SingleOrDefaultAsync(redirect => redirect.LanguageCode == languageCode && redirect.FromPath == fromPath && redirect.IsActive, cancellationToken);

    public Task<Redirect?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Redirects.SingleOrDefaultAsync(redirect => redirect.Id == id, cancellationToken);

    public async Task<bool> FromPathExistsAsync(string languageCode, string fromPath, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.Redirects.Local.Any(redirect => redirect.LanguageCode == languageCode && redirect.FromPath == fromPath && redirect.Id != excludeId)
        || await dbContext.Redirects.AnyAsync(redirect => redirect.LanguageCode == languageCode && redirect.FromPath == fromPath && redirect.Id != excludeId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<PagedResult<Redirect>> ListAsync(string? languageCode, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Redirects.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            query = query.Where(redirect => redirect.LanguageCode == languageCode);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());
            query = query.Where(redirect => EF.Functions.ILike(redirect.FromPath, pattern, LikePattern.EscapeCharacter));
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(redirect => redirect.LanguageCode).ThenBy(redirect => redirect.FromPath)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<Redirect>(items, page, pageSize, total);
    }

    public Task<bool> TargetsContentAsync(Guid contentItemId, CancellationToken cancellationToken) =>
        dbContext.Redirects.AnyAsync(redirect => redirect.TargetContentItemId == contentItemId, cancellationToken);

    public void Add(Redirect redirect) => dbContext.Redirects.Add(redirect);

    public void Remove(Redirect redirect) => dbContext.Redirects.Remove(redirect);
}
