using Microsoft.EntityFrameworkCore;
using Onyx.Application.Brand;
using Onyx.Application.Content;
using Onyx.Application.Email;
using Onyx.Application.Intelligence;
using Onyx.Application.Knowledge;
using Onyx.Application.Media;
using Onyx.Contracts.Common;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class CallToActionRepository(ApplicationDbContext dbContext) : ICallToActionRepository
{
    public Task<CallToAction?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.CallToActions.SingleOrDefaultAsync(cta => cta.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, CallToAction>> GetLookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0
            ? new Dictionary<Guid, CallToAction>()
            : await dbContext.CallToActions.AsNoTracking().Where(cta => ids.Contains(cta.Id)).ToDictionaryAsync(cta => cta.Id, cancellationToken).ConfigureAwait(false);

    public Task<bool> KeyExistsAsync(string key, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.CallToActions.AnyAsync(cta => cta.Key == key && cta.Id != excludeId, cancellationToken);

    public async Task<IReadOnlyList<CallToAction>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.CallToActions.AsNoTracking().OrderBy(cta => cta.Key).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(CallToAction cta) => dbContext.CallToActions.Add(cta);

    public void SetExpectedVersion(CallToAction cta, uint version) => dbContext.Entry(cta).Property(entry => entry.Version).OriginalValue = version;

    public async Task<IReadOnlyList<CtaDefault>> ListDefaultsAsync(CancellationToken cancellationToken) =>
        await dbContext.CtaDefaults.ToListAsync(cancellationToken).ConfigureAwait(false);

    public void AddDefault(CtaDefault ctaDefault) => dbContext.CtaDefaults.Add(ctaDefault);

    public void RemoveDefault(CtaDefault ctaDefault) => dbContext.CtaDefaults.Remove(ctaDefault);
}
