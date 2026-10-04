using Onyx.Contracts.Common;
using Onyx.Domain.Intelligence;

namespace Onyx.Application.Intelligence;

public interface IKeywordTargetRepository
{
    Task<KeywordTarget?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<KeywordTarget>> ListAsync(string? languageCode, KeywordStatus? status, string? search, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Every map row for this keyword (comparison form) in this language, across markets.</summary>
    Task<IReadOnlyList<KeywordTarget>> FindAsync(string languageCode, string normalizedKeyword, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string languageCode, string normalizedKeyword, string targetMarket, Guid? excludeId, CancellationToken cancellationToken);

    void Add(KeywordTarget target);

    void Remove(KeywordTarget target);

    void SetExpectedVersion(KeywordTarget target, uint version);
}
