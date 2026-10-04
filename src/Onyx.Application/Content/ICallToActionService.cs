using Onyx.Contracts.Content;
using Onyx.Domain.Common;

namespace Onyx.Application.Content;

public interface ICallToActionService
{
    Task<IReadOnlyList<CtaResponse>> ListAsync(CancellationToken cancellationToken);

    Task<Result<CtaResponse>> CreateAsync(SaveCtaRequest request, CancellationToken cancellationToken);

    Task<Result<CtaResponse>> UpdateAsync(Guid id, SaveCtaRequest request, CancellationToken cancellationToken);

    Task<CtaDefaultsResponse> GetDefaultsAsync(CancellationToken cancellationToken);

    Task<Result<CtaDefaultsResponse>> SetDefaultAsync(SetCtaDefaultRequest request, CancellationToken cancellationToken);
}
