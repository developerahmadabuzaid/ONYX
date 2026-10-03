using Onyx.Application.Privacy;
using Onyx.Domain.Privacy;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Privacy;

internal sealed class CookieConsentRepository(ApplicationDbContext dbContext) : ICookieConsentRepository
{
    public void Add(CookieConsentReceipt receipt) => dbContext.CookieConsentReceipts.Add(receipt);
}
