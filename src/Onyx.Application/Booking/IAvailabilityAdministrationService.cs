using Onyx.Contracts.Booking;
using Onyx.Domain.Common;

namespace Onyx.Application.Booking;

public interface IAvailabilityAdministrationService
{
    Task<BookingSettingsResponse> GetAsync(CancellationToken cancellationToken);

    Task<Result<BookingSettingsResponse>> SaveSettingsAsync(SaveBookingSettingsRequest request, CancellationToken cancellationToken);

    Task<Result<BookingSettingsResponse>> AddRuleAsync(SaveAvailabilityRuleRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteRuleAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<BookingSettingsResponse>> AddExceptionAsync(SaveAvailabilityExceptionRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteExceptionAsync(Guid id, CancellationToken cancellationToken);
}
