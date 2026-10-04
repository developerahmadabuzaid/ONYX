using Onyx.Domain.Brand;

namespace Onyx.Application.Brand;

public interface IBrandKitRepository
{
    /// <summary>The single Brand Kit row (tracked), or null before it is first saved.</summary>
    Task<BrandKit?> GetAsync(CancellationToken cancellationToken);

    void Add(BrandKit brandKit);

    void SetExpectedVersion(BrandKit brandKit, uint version);
}
