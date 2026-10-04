using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Domain.Common;

namespace Onyx.Application.Content;

public interface IRedirectAdministrationService
{
    Task<PagedResult<RedirectResponse>> ListAsync(string? languageCode, string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<Result<RedirectResponse>> CreateAsync(SaveRedirectRequest request, CancellationToken cancellationToken);

    Task<Result<RedirectResponse>> UpdateAsync(Guid id, SaveRedirectRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
