using Onyx.Domain.Privacy;

namespace Onyx.Application.Privacy;

public interface ICookieConsentRepository
{
    void Add(CookieConsentReceipt receipt);
}
