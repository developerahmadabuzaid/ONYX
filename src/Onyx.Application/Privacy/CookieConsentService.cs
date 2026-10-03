using FluentValidation;
using Onyx.Application.Abstractions;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Common;
using Onyx.Contracts.Privacy;
using Onyx.Domain.Common;
using Onyx.Domain.Privacy;

namespace Onyx.Application.Privacy;

public sealed class CookieConsentService(
    ICookieConsentRepository repository,
    IUnitOfWork unitOfWork,
    IIpAddressHasher ipHasher,
    IRequestContext requestContext,
    TimeProvider timeProvider,
    IValidator<RecordCookieConsentRequest> validator) : ICookieConsentService
{
    public async Task<Result<Guid>> RecordAsync(RecordCookieConsentRequest request, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var receipt = CookieConsentReceipt.Record(
            request.ConsentId,
            request.PolicyVersion,
            request.Analytics,
            request.Marketing,
            request.LanguageCode,
            timeProvider.GetUtcNow(),
            ipHasher.Hash(requestContext.IpAddress));

        repository.Add(receipt);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return receipt.Id;
    }
}
