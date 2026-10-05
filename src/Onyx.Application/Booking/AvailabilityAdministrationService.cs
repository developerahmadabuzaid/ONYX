using System.Globalization;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Contracts.Booking;
using Onyx.Domain.Auditing;
using Onyx.Domain.Booking;
using Onyx.Domain.Common;

namespace Onyx.Application.Booking;

/// <summary>
/// Admin-managed internal availability. Working days, hours, slot length, buffer, notice and horizon are business inputs (still
/// open in architecture.md §13.4), so nothing is preset: booking stays closed until an administrator enters and enables them.
/// </summary>
public sealed class AvailabilityAdministrationService(
    IAvailabilityRepository availability,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IAvailabilityAdministrationService
{
    private const string TimeFormat = "HH:mm";

    public async Task<BookingSettingsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await availability.GetSettingsAsync(cancellationToken).ConfigureAwait(false) ?? BookingSettings.CreateDisabled();
        return await ToResponseAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<BookingSettingsResponse>> SaveSettingsAsync(SaveBookingSettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = await availability.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (settings is null)
        {
            settings = BookingSettings.CreateDisabled();
            availability.AddSettings(settings);
        }
        else if (request.Version is { } version)
        {
            availability.SetExpectedVersion(settings, version);
        }

        var updated = settings.Update(
            request.IsEnabled, request.TimeZoneId, request.SlotMinutes, request.BufferMinutes, request.MinimumNoticeHours, request.HorizonDays);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        auditTrail.Record(AuditAction.BookingSettingsChanged, AuditCategory.Leads, nameof(BookingSettings), settings.Id.ToString(), new { enabled = settings.IsEnabled });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToResponseAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<BookingSettingsResponse>> AddRuleAsync(SaveAvailabilityRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<DayOfWeek>(request.DayOfWeek, out var day)
            || !TimeOnly.TryParseExact(request.StartTime, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !TimeOnly.TryParseExact(request.EndTime, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
        {
            return Error.Validation("Availability.InvalidRule", "Use a day name and HH:mm start and end times.");
        }

        var rule = AvailabilityRule.Create(day, start, end);
        if (rule.IsFailure)
        {
            return rule.Error!;
        }

        var existing = await availability.ListRulesAsync(cancellationToken).ConfigureAwait(false);
        if (existing.Any(other => other.Overlaps(rule.Value)))
        {
            return Error.Conflict("Availability.Overlap", "This window overlaps another window on the same day.");
        }

        availability.AddRule(rule.Value);
        auditTrail.Record(AuditAction.AvailabilityChanged, AuditCategory.Leads, nameof(AvailabilityRule), rule.Value.Id.ToString(), new { change = "rule-added" });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> DeleteRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await availability.GetRuleAsync(id, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return Error.NotFound("Availability.RuleNotFound", "Availability window not found.");
        }

        availability.RemoveRule(rule);
        auditTrail.Record(AuditAction.AvailabilityChanged, AuditCategory.Leads, nameof(AvailabilityRule), id.ToString(), new { change = "rule-removed" });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<BookingSettingsResponse>> AddExceptionAsync(SaveAvailabilityExceptionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Date < DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(-1))
        {
            return Error.Validation("Availability.PastDate", "Blocked dates must be today or later.");
        }

        if (await availability.ExceptionExistsAsync(request.Date, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Availability.DateBlocked", "This date is already blocked.");
        }

        var exception = AvailabilityException.Create(request.Date, request.Reason);
        if (exception.IsFailure)
        {
            return exception.Error!;
        }

        availability.AddException(exception.Value);
        auditTrail.Record(AuditAction.AvailabilityChanged, AuditCategory.Leads, nameof(AvailabilityException), exception.Value.Id.ToString(), new { change = "date-blocked" });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> DeleteExceptionAsync(Guid id, CancellationToken cancellationToken)
    {
        var exception = await availability.GetExceptionAsync(id, cancellationToken).ConfigureAwait(false);
        if (exception is null)
        {
            return Error.NotFound("Availability.ExceptionNotFound", "Blocked date not found.");
        }

        availability.RemoveException(exception);
        auditTrail.Record(AuditAction.AvailabilityChanged, AuditCategory.Leads, nameof(AvailabilityException), id.ToString(), new { change = "date-unblocked" });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<BookingSettingsResponse> ToResponseAsync(BookingSettings settings, CancellationToken cancellationToken)
    {
        var rules = await availability.ListRulesAsync(cancellationToken).ConfigureAwait(false);
        var exceptions = await availability.ListExceptionsAsync(DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(-1), cancellationToken).ConfigureAwait(false);
        return new BookingSettingsResponse(
            settings.IsEnabled,
            settings.IsBookable && rules.Count > 0,
            settings.TimeZoneId,
            settings.SlotMinutes,
            settings.BufferMinutes,
            settings.MinimumNoticeHours,
            settings.HorizonDays,
            settings.Version,
            rules.OrderBy(rule => rule.DayOfWeek).ThenBy(rule => rule.StartTime)
                .Select(rule => new AvailabilityRuleResponse(
                    rule.Id, rule.DayOfWeek.ToString(), rule.StartTime.ToString(TimeFormat, CultureInfo.InvariantCulture), rule.EndTime.ToString(TimeFormat, CultureInfo.InvariantCulture)))
                .ToList(),
            exceptions.OrderBy(exception => exception.Date).Select(exception => new AvailabilityExceptionResponse(exception.Id, exception.Date, exception.Reason)).ToList());
    }
}
