using Onyx.Contracts.Brand;
using Onyx.Domain.Common;

namespace Onyx.Application.Brand;

public interface IBrandKitService
{
    Task<BrandKitResponse> GetAsync(CancellationToken cancellationToken);

    Task<Result<BrandKitResponse>> UpdateAsync(UpdateBrandKitRequest request, CancellationToken cancellationToken);
}
