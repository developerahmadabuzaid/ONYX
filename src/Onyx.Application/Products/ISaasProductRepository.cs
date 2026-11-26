using Onyx.Domain.Products;

namespace Onyx.Application.Products;

public interface ISaasProductRepository
{
    Task<SaasProduct?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The published product with this slug in this language, with translations and plans.</summary>
    Task<SaasProduct?> GetPublishedBySlugAsync(string language, string slug, CancellationToken cancellationToken);

    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken);

    void Add(SaasProduct product);

    void SetExpectedVersion(SaasProduct product, uint version);
}
