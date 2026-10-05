using Microsoft.EntityFrameworkCore;
using Onyx.Application.Booking;
using Onyx.Domain.Booking;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class AvailabilityRepository(ApplicationDbContext dbContext) : IAvailabilityRepository
{
    public Task<BookingSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
        dbContext.BookingSettings.SingleOrDefaultAsync(settings => settings.Key == BookingSettings.SingletonKey, cancellationToken);

    public async Task<IReadOnlyList<AvailabilityRule>> ListRulesAsync(CancellationToken cancellationToken) =>
        await dbContext.AvailabilityRules.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<AvailabilityException>> ListExceptionsAsync(DateOnly fromDate, CancellationToken cancellationToken) =>
        await dbContext.AvailabilityExceptions.AsNoTracking().Where(exception => exception.Date >= fromDate).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<AvailabilityRule?> GetRuleAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.AvailabilityRules.SingleOrDefaultAsync(rule => rule.Id == id, cancellationToken);

    public Task<AvailabilityException?> GetExceptionAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.AvailabilityExceptions.SingleOrDefaultAsync(exception => exception.Id == id, cancellationToken);

    public Task<bool> ExceptionExistsAsync(DateOnly date, CancellationToken cancellationToken) =>
        dbContext.AvailabilityExceptions.AnyAsync(exception => exception.Date == date, cancellationToken);

    public void AddSettings(BookingSettings settings) => dbContext.BookingSettings.Add(settings);

    public void AddRule(AvailabilityRule rule) => dbContext.AvailabilityRules.Add(rule);

    public void RemoveRule(AvailabilityRule rule) => dbContext.AvailabilityRules.Remove(rule);

    public void AddException(AvailabilityException exception) => dbContext.AvailabilityExceptions.Add(exception);

    public void RemoveException(AvailabilityException exception) => dbContext.AvailabilityExceptions.Remove(exception);

    public void SetExpectedVersion(BookingSettings settings, uint version) =>
        dbContext.Entry(settings).Property(entry => entry.Version).OriginalValue = version;
}
