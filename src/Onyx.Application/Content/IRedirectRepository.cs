using Onyx.Contracts.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

public interface IRedirectRepository
{
    Task<Redirect?> FindActiveAsync(string languageCode, string fromPath, CancellationToken cancellationToken);

    Task<Redirect?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> FromPathExistsAsync(string languageCode, string fromPath, Guid? excludeId, CancellationToken cancellationToken);

    Task<PagedResult<Redirect>> ListAsync(string? languageCode, string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<bool> TargetsContentAsync(Guid contentItemId, CancellationToken cancellationToken);

    void Add(Redirect redirect);

    void Remove(Redirect redirect);
}
