using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Common;
using Onyx.Contracts.Leads;
using Onyx.Domain.Common;
using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

/// <summary>
/// Public form submissions (architecture.md §13.3): one transaction stores Inquiry + ConsentRecord + Lead (create or match) and
/// the InquirySubmitted outbox event; the Worker then sends the emails and the admin notification.
/// </summary>
public sealed class InquiryService(InquiryIntake intake, InquiryRecorder recorder, IUnitOfWork unitOfWork) : IInquiryService
{
    public const string CaptchaAction = "inquiry";

    public async Task<Result<InquiryAcceptedResponse>> SubmitAsync(SubmitInquiryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Form is null || request.Type is null || !InquiryTypes.PublicForms.Contains(request.Type)
            || !EnumParsing.TryParse<InquiryType>(request.Type, out var type))
        {
            return Error.Validation("Inquiry.UnsupportedType", "This kind of inquiry cannot be submitted through the form endpoint.");
        }

        var prepared = await intake.PrepareAsync(type, request.Form, CaptchaAction, cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return prepared.Error!;
        }

        if (prepared.Value.IsSpam)
        {
            return new InquiryAcceptedResponse(InquiryRecorder.Reference(Guid.CreateVersion7()));
        }

        var recorded = await recorder.RecordAsync(prepared.Value, cancellationToken).ConfigureAwait(false);
        if (recorded.IsFailure)
        {
            return recorded.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new InquiryAcceptedResponse(InquiryRecorder.Reference(recorded.Value.Inquiry.Id));
    }
}
