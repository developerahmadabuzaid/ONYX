using Onyx.Domain.Privacy;

namespace Onyx.Application.Privacy;

public interface IRetentionPolicyRepository
{
    Task<IReadOnlyList<RetentionPolicy>> ListAsync(CancellationToken cancellationToken);

    Task<RetentionPolicy?> GetAsync(RetentionCategory category, CancellationToken cancellationToken);

    void Add(RetentionPolicy policy);

    void SetExpectedVersion(RetentionPolicy policy, uint version);
}
