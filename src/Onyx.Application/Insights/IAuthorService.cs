using Onyx.Contracts.Insights;
using Onyx.Domain.Common;

namespace Onyx.Application.Insights;

/// <summary>Real people who may be credited on insights (permissions are enforced by the API: <c>Insights.View</c> / <c>Insights.Edit</c>).</summary>
public interface IAuthorService
{
    Task<IReadOnlyList<AuthorResponse>> ListAsync(CancellationToken cancellationToken);

    Task<Result<AuthorResponse>> CreateAsync(SaveAuthorRequest request, CancellationToken cancellationToken);

    Task<Result<AuthorResponse>> UpdateAsync(Guid id, SaveAuthorRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
