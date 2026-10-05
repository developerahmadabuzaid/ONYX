using Onyx.Domain.Booking;

namespace Onyx.Application.Booking;

/// <summary>The internal availability aggregate: the singleton booking settings, weekly windows and blocked dates.</summary>
public interface IAvailabilityRepository
{
    /// <summary>The tracked settings row, or null before an administrator first saves them.</summary>
    Task<BookingSettings?> GetSettingsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AvailabilityRule>> ListRulesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AvailabilityException>> ListExceptionsAsync(DateOnly fromDate, CancellationToken cancellationToken);

    Task<AvailabilityRule?> GetRuleAsync(Guid id, CancellationToken cancellationToken);

    Task<AvailabilityException?> GetExceptionAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExceptionExistsAsync(DateOnly date, CancellationToken cancellationToken);

    void AddSettings(BookingSettings settings);

    void AddRule(AvailabilityRule rule);

    void RemoveRule(AvailabilityRule rule);

    void AddException(AvailabilityException exception);

    void RemoveException(AvailabilityException exception);

    void SetExpectedVersion(BookingSettings settings, uint version);
}
