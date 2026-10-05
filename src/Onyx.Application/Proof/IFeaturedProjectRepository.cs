using Onyx.Domain.Proof;

namespace Onyx.Application.Proof;

public interface IFeaturedProjectRepository
{
    Task<IReadOnlyList<FeaturedProject>> ListAsync(CancellationToken cancellationToken);

    Task<FeaturedProject?> FindForContentAsync(Guid contentItemId, CancellationToken cancellationToken);

    void Add(FeaturedProject featured);

    void Remove(FeaturedProject featured);
}
