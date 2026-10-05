using Microsoft.EntityFrameworkCore;
using Onyx.Application.Privacy;
using Onyx.Domain.Privacy;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class RetentionPolicyRepository(ApplicationDbContext dbContext) : IRetentionPolicyRepository
{
    public async Task<IReadOnlyList<RetentionPolicy>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.RetentionPolicies.ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<RetentionPolicy?> GetAsync(RetentionCategory category, CancellationToken cancellationToken) =>
        dbContext.RetentionPolicies.SingleOrDefaultAsync(policy => policy.Category == category, cancellationToken);

    public void Add(RetentionPolicy policy) => dbContext.RetentionPolicies.Add(policy);

    public void SetExpectedVersion(RetentionPolicy policy, uint version) =>
        dbContext.Entry(policy).Property(entry => entry.Version).OriginalValue = version;
}
