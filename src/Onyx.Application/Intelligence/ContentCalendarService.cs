using Onyx.Application.Abstractions;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Content;
using Onyx.Application.Localization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Intelligence;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;

namespace Onyx.Application.Intelligence;

/// <summary>
/// Content calendar: proposal → admin decision → preparation (linked draft) → normal review/approval/scheduling. Editor dates
/// are recorded as manual choices; data-backed timing will come only from connected analytics.
/// </summary>
public sealed class ContentCalendarService(
    IContentCalendarRepository calendar,
    IContentRepository content,
    ICurrentUser currentUser,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IContentCalendarService
{
    public const int MaxPageSize = 100;

    private static readonly Error NotFound = Error.NotFound("Calendar.NotFound", "Calendar entry not found.");

    public async Task<Result<PagedResult<CalendarEntryResponse>>> ListAsync(string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        CalendarEntryStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!EnumParsing.TryParse<CalendarEntryStatus>(status, out var parsed))
            {
                return Error.Validation("Calendar.InvalidStatus", "Unknown status.");
            }

            filter = parsed;
        }

        var result = await calendar.ListAsync(filter, Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), cancellationToken).ConfigureAwait(false);
        return new PagedResult<CalendarEntryResponse>(result.Items.Select(ToResponse).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<Result<CalendarEntryResponse>> ProposeAsync(ProposeCalendarEntryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SupportedLanguages.All.Contains(request.LanguageCode))
        {
            return Error.Validation("Calendar.UnsupportedLanguage", "Unsupported language.");
        }

        var intent = SearchIntent.Unspecified;
        if (!string.IsNullOrWhiteSpace(request.SearchIntent) && !EnumParsing.TryParse(request.SearchIntent, out intent))
        {
            return Error.Validation("Calendar.InvalidIntent", "Unknown search intent.");
        }

        var referenced = (request.SupportingContentIds ?? []).Concat(request.TargetServiceId is { } service ? [service] : []).Distinct().ToList();
        var found = await content.GetLookupAsync(referenced, cancellationToken).ConfigureAwait(false);
        if (found.Count != referenced.Count)
        {
            return Error.Validation("Calendar.InvalidReference", "Some referenced content does not exist.");
        }

        var proposed = ContentCalendarEntry.Propose(
            request.Topic, request.LanguageCode, request.TargetMarket?.Trim().ToUpperInvariant(), request.TargetServiceId, request.ClusterId, request.Keyword, intent,
            request.Reason, request.SupportingContentIds ?? [], request.ProposedPublishAt,
            request.ProposedPublishAt is null ? RecommendationBasis.InsufficientData : RecommendationBasis.Manual, [], RecommendationOrigin.Editor);
        if (proposed.IsFailure)
        {
            return proposed.Error!;
        }

        calendar.Add(proposed.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(proposed.Value);
    }

    public Task<Result<CalendarEntryResponse>> AcceptAsync(Guid id, DecideRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request?.Version, entry => entry.Accept(UserId(), request?.Note, timeProvider.GetUtcNow()), audit: true, cancellationToken);

    public Task<Result<CalendarEntryResponse>> RejectAsync(Guid id, DecideRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request?.Version, entry => entry.Reject(UserId(), request?.Note, timeProvider.GetUtcNow()), audit: true, cancellationToken);

    public async Task<Result<CalendarEntryResponse>> LinkContentAsync(Guid id, LinkCalendarContentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!(await content.GetLookupAsync([request.ContentItemId], cancellationToken).ConfigureAwait(false)).ContainsKey(request.ContentItemId))
        {
            return ContentErrors.NotFound;
        }

        return await ChangeAsync(id, request.Version, entry => entry.LinkContent(request.ContentItemId), audit: false, cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<CalendarEntryResponse>> CompleteAsync(Guid id, DecideRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request?.Version, entry => entry.Complete(), audit: false, cancellationToken);

    private async Task<Result<CalendarEntryResponse>> ChangeAsync(
        Guid id, uint? version, Func<ContentCalendarEntry, Result> change, bool audit, CancellationToken cancellationToken)
    {
        var entry = await calendar.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return NotFound;
        }

        if (version is { } expected)
        {
            calendar.SetExpectedVersion(entry, expected);
        }

        var result = change(entry);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        if (audit)
        {
            auditTrail.Record(AuditAction.CalendarEntryDecided, AuditCategory.Content, nameof(ContentCalendarEntry), entry.Id.ToString(),
                new { status = entry.Status.ToString(), entry.Topic });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(entry);
    }

    private Guid UserId() => currentUser.UserId ?? throw new InvalidOperationException("An authenticated admin is required.");

    private static CalendarEntryResponse ToResponse(ContentCalendarEntry entry) => new(
        entry.Id, entry.Topic, entry.LanguageCode, entry.TargetMarket, entry.TargetServiceId, entry.ClusterId, entry.Keyword, entry.SearchIntent.ToString(),
        entry.Reason, entry.SupportingContentIds, entry.ProposedPublishAtUtc, entry.TimingBasis.ToString(), entry.Evidence.Select(IntelligenceMapping.ToDto).ToList(),
        entry.Origin.ToString(), entry.Status.ToString(), entry.LinkedContentItemId, entry.DecisionNote, entry.CreatedAtUtc, entry.Version);
}
