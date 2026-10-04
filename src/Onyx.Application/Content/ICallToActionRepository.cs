using Onyx.Domain.Content;

namespace Onyx.Application.Content;

public interface ICallToActionRepository
{
    Task<CallToAction?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, CallToAction>> GetLookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<bool> KeyExistsAsync(string key, Guid? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CallToAction>> ListAsync(CancellationToken cancellationToken);

    void Add(CallToAction cta);

    void SetExpectedVersion(CallToAction cta, uint version);

    Task<IReadOnlyList<CtaDefault>> ListDefaultsAsync(CancellationToken cancellationToken);

    void AddDefault(CtaDefault ctaDefault);

    void RemoveDefault(CtaDefault ctaDefault);
}
