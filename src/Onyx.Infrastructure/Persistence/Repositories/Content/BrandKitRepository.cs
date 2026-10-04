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

internal sealed class BrandKitRepository(ApplicationDbContext dbContext) : IBrandKitRepository
{
    public Task<BrandKit?> GetAsync(CancellationToken cancellationToken) =>
        dbContext.BrandKits.OrderBy(kit => kit.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);

    public void Add(BrandKit brandKit) => dbContext.BrandKits.Add(brandKit);

    public void SetExpectedVersion(BrandKit brandKit, uint version) => dbContext.Entry(brandKit).Property(entry => entry.Version).OriginalValue = version;
}
