using Onyx.Contracts.Common;
using Onyx.Contracts.Intelligence;
using Onyx.Domain.Common;

namespace Onyx.Application.Intelligence;

public interface IKeywordMapService
{
    Task<Result<PagedResult<KeywordTargetResponse>>> ListAsync(string? languageCode, string? status, string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<Result<KeywordTargetResponse>> CreateAsync(SaveKeywordTargetRequest request, CancellationToken cancellationToken);

    Task<Result<KeywordTargetResponse>> UpdateAsync(Guid id, SaveKeywordTargetRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Before creating new content: is this keyword (comparison form) already owned in this language?</summary>
    Task<Result<KeywordConflictResponse>> CheckConflictsAsync(string languageCode, string keyword, Guid? excludeContentItemId, CancellationToken cancellationToken);
}
