using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Booking;
using Onyx.Application.Common;
using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

/// <summary>
/// The Admin lead pipeline (architecture.md §13.2). Endpoint permissions are enforced by the API; this service adds the
/// per-lead visibility rule and the assignee rule, and audits stage, owner, priority, note, consent and export actions
/// without copying contact details into the audit log.
/// </summary>
public sealed class LeadAdministrationService(
    ILeadRepository leads,
    IInquiryRepository inquiries,
    IBookingRepository bookings,
    IUserDirectory users,
    ICurrentAdmin currentAdmin,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ILeadAdministrationService
{
    public const int MaxPageSize = 100;
    public const int MaxExportRows = 5_000;

    private static readonly Error NotFound = Error.NotFound("Lead.NotFound", "Lead not found.");

    public async Task<Result<PagedResult<LeadSummaryResponse>>> ListAsync(LeadListQuery query, CancellationToken cancellationToken)
    {
        var filter = await BuildFilterAsync(query, cancellationToken).ConfigureAwait(false);
        if (filter.IsFailure)
        {
            return filter.Error!;
        }

        var page = await leads.ListAsync(filter.Value, cancellationToken).ConfigureAwait(false);
        var names = await OwnerNamesAsync(page.Items, cancellationToken).ConfigureAwait(false);
        return new PagedResult<LeadSummaryResponse>(page.Items.Select(lead => LeadMapping.ToSummary(lead, names)).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<Result<LeadDetailResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var lead = await GetVisibleAsync(id, cancellationToken).ConfigureAwait(false);
        if (lead is null)
        {
            return NotFound;
        }

        var leadInquiries = await inquiries.ListForLeadAsync(id, cancellationToken).ConfigureAwait(false);
        var consents = (await inquiries.ListConsentsForLeadAsync(id, cancellationToken).ConfigureAwait(false)).ToDictionary(consent => consent.InquiryId);
        var notes = await leads.ListNotesAsync(id, cancellationToken).ConfigureAwait(false);
        var activities = await leads.ListActivitiesAsync(id, cancellationToken).ConfigureAwait(false);
        var leadBookings = await bookings.ListForLeadAsync(id, cancellationToken).ConfigureAwait(false);

        var userIds = notes.Select(note => note.AuthorId).Concat(lead.OwnerId is { } owner ? [owner] : []).Distinct().ToList();
        var names = await users.GetDisplayNamesAsync(userIds, cancellationToken).ConfigureAwait(false);

        return new LeadDetailResponse(
            LeadMapping.ToSummary(lead, names),
            lead.PillarId,
            lead.ScoreReasons,
            lead.ClosedAtUtc,
            leadInquiries.OrderByDescending(inquiry => inquiry.SubmittedAtUtc)
                .Select(inquiry => LeadMapping.ToResponse(inquiry, consents.GetValueOrDefault(inquiry.Id))).ToList(),
            notes.OrderByDescending(note => note.CreatedAtUtc)
                .Select(note => new LeadNoteResponse(note.Id, note.Body, note.AuthorId, names.GetValueOrDefault(note.AuthorId), note.CreatedAtUtc)).ToList(),
            activities.OrderByDescending(activity => activity.OccurredAtUtc).Select(LeadMapping.ToResponse).ToList(),
            leadBookings.OrderByDescending(booking => booking.StartUtc)
                .Select(booking => new LeadBookingResponse(booking.Id, booking.StartUtc, booking.EndUtc, booking.TimeZoneId, booking.Status.ToString())).ToList());
    }

    public async Task<Result<LeadSummaryResponse>> ChangeStageAsync(Guid id, ChangeLeadStageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<LeadStage>(request.Stage, out var stage))
        {
            return Error.Validation("Lead.InvalidStage", "Unknown pipeline stage.");
        }

        return await UpdateAsync(id, request.Version, (lead, actor, now) =>
        {
            var previous = lead.Stage;
            var changed = lead.ChangeStage(stage, actor, now);
            if (changed.IsSuccess && previous != stage)
            {
                auditTrail.Record(AuditAction.LeadStageChanged, AuditCategory.Leads, nameof(Lead), lead.Id.ToString(), new { from = previous.ToString(), to = stage.ToString() });
            }

            return Task.FromResult(changed);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<LeadSummaryResponse>> ChangePriorityAsync(Guid id, ChangeLeadPriorityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<LeadPriority>(request.Priority, out var priority))
        {
            return Error.Validation("Lead.InvalidPriority", "Unknown priority.");
        }

        return await UpdateAsync(id, request.Version, (lead, actor, now) =>
        {
            var changed = lead.ChangePriority(priority, actor, now);
            if (changed.IsSuccess)
            {
                auditTrail.Record(AuditAction.LeadPriorityChanged, AuditCategory.Leads, nameof(Lead), lead.Id.ToString(), new { priority = priority.ToString() });
            }

            return Task.FromResult(changed);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The new owner must be an active user who can see leads (<c>Leads.View</c>).</summary>
    public async Task<Result<LeadSummaryResponse>> AssignAsync(Guid id, AssignLeadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OwnerId is { } ownerId)
        {
            var assignees = await users.ListWithPermissionAsync(Permissions.Leads.View, cancellationToken).ConfigureAwait(false);
            if (assignees.All(user => user.UserId != ownerId))
            {
                return Error.Validation("Lead.InvalidOwner", "Leads can be assigned only to active users who can view leads.");
            }
        }

        return await UpdateAsync(id, request.Version, (lead, actor, now) =>
        {
            if (lead.OwnerId != request.OwnerId)
            {
                lead.AssignTo(request.OwnerId, actor, now);
                auditTrail.Record(AuditAction.LeadAssigned, AuditCategory.Leads, nameof(Lead), lead.Id.ToString(), new { owner = request.OwnerId });
            }

            return Task.FromResult(Result.Success());
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<LeadNoteResponse>> AddNoteAsync(Guid id, AddLeadNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        var lead = await GetVisibleAsync(id, actor, cancellationToken).ConfigureAwait(false);
        if (lead is null)
        {
            return NotFound;
        }

        var now = timeProvider.GetUtcNow();
        var note = LeadNote.Write(lead.Id, request.Body, actor.UserId, now);
        if (note.IsFailure)
        {
            return note.Error!;
        }

        leads.AddNote(note.Value);
        lead.RecordActivity(LeadActivityKind.NoteAdded, null, note.Value.Id, actor.UserId, now);
        leads.AddPendingActivities(lead);
        auditTrail.Record(AuditAction.LeadNoteAdded, AuditCategory.Leads, nameof(Lead), lead.Id.ToString(), new { note = note.Value.Id });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var names = await users.GetDisplayNamesAsync([actor.UserId], cancellationToken).ConfigureAwait(false);
        return new LeadNoteResponse(note.Value.Id, note.Value.Body, actor.UserId, names.GetValueOrDefault(actor.UserId), note.Value.CreatedAtUtc);
    }

    /// <summary>Records a visitor request (received by staff) to stop marketing. Contact consent for the request itself is unchanged.</summary>
    public async Task<Result> WithdrawMarketingConsentAsync(Guid id, Guid consentId, CancellationToken cancellationToken)
    {
        var actor = await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        var lead = await GetVisibleAsync(id, actor, cancellationToken).ConfigureAwait(false);
        var consent = lead is null ? null : await inquiries.GetConsentAsync(consentId, cancellationToken).ConfigureAwait(false);
        if (lead is null || consent is null || consent.LeadId != lead.Id)
        {
            return Result.Failure(NotFound);
        }

        var now = timeProvider.GetUtcNow();
        var withdrawn = consent.WithdrawMarketing(actor.UserId, now);
        if (withdrawn.IsFailure)
        {
            return withdrawn;
        }

        lead.RecordActivity(LeadActivityKind.MarketingConsentWithdrawn, null, consent.Id, actor.UserId, now);
        leads.AddPendingActivities(lead);
        auditTrail.Record(AuditAction.MarketingConsentWithdrawn, AuditCategory.Privacy, nameof(ConsentRecord), consent.Id.ToString(), new { lead = lead.Id });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<IReadOnlyList<LeadAssigneeResponse>> ListAssigneesAsync(CancellationToken cancellationToken) =>
        (await users.ListWithPermissionAsync(Permissions.Leads.View, cancellationToken).ConfigureAwait(false))
            .Select(user => new LeadAssigneeResponse(user.UserId, user.DisplayName)).ToList();

    public async Task<Result<PagedResult<InquirySummaryResponse>>> ListInquiriesAsync(InquiryListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        InquiryType? type = null;
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            if (!EnumParsing.TryParse<InquiryType>(query.Type, out var parsed))
            {
                return Error.Validation("Inquiry.InvalidType", "Unknown inquiry type.");
            }

            type = parsed;
        }

        var actor = await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        var page = await inquiries.ListAsync(type, ScopeFor(actor), Math.Max(query.Page, 1), Math.Clamp(query.PageSize, 1, MaxPageSize), cancellationToken).ConfigureAwait(false);
        return new PagedResult<InquirySummaryResponse>(
            page.Items.Select(inquiry => new InquirySummaryResponse(inquiry.Id, inquiry.LeadId, inquiry.Type.ToString(), inquiry.LanguageCode, inquiry.Name, inquiry.Company, inquiry.SubmittedAtUtc)).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    /// <summary>Exports the leads the caller can see (filters applied), capped at <see cref="MaxExportRows"/>. Audited without contact details.</summary>
    public async Task<Result<LeadExportFile>> ExportAsync(LeadListQuery query, CancellationToken cancellationToken)
    {
        var filter = await BuildFilterAsync(query, cancellationToken).ConfigureAwait(false);
        if (filter.IsFailure)
        {
            return filter.Error!;
        }

        var rows = await leads.ListForExportAsync(filter.Value, MaxExportRows, cancellationToken).ConfigureAwait(false);
        var names = await OwnerNamesAsync(rows, cancellationToken).ConfigureAwait(false);
        var marketing = await inquiries.ListLeadsWithMarketingConsentAsync(rows.Select(lead => lead.Id).ToList(), cancellationToken).ConfigureAwait(false);
        var content = LeadCsv.Write(rows, names, marketing);

        auditTrail.Record(
            AuditAction.LeadsExported,
            AuditCategory.Leads,
            nameof(Lead),
            null,
            new { rows = rows.Count, stage = filter.Value.Stage?.ToString(), owner = query?.Owner, searched = !string.IsNullOrWhiteSpace(filter.Value.Search), capped = rows.Count >= MaxExportRows });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var stamp = timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture);
        return new LeadExportFile($"onyx-leads-{stamp}.csv", content, rows.Count);
    }

    private async Task<Result<LeadSummaryResponse>> UpdateAsync(
        Guid id, uint? version, Func<Lead, Guid, DateTimeOffset, Task<Result>> change, CancellationToken cancellationToken)
    {
        var actor = await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        var lead = await GetVisibleAsync(id, actor, cancellationToken).ConfigureAwait(false);
        if (lead is null)
        {
            return NotFound;
        }

        if (version is { } expected)
        {
            leads.SetExpectedVersion(lead, expected);
        }

        var changed = await change(lead, actor.UserId, timeProvider.GetUtcNow()).ConfigureAwait(false);
        if (changed.IsFailure)
        {
            return changed.Error!;
        }

        leads.AddPendingActivities(lead);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LeadMapping.ToSummary(lead, await OwnerNamesAsync([lead], cancellationToken).ConfigureAwait(false));
    }

    private async Task<Result<LeadFilter>> BuildFilterAsync(LeadListQuery? query, CancellationToken cancellationToken)
    {
        query ??= new LeadListQuery();
        LeadStage? stage = null;
        if (!string.IsNullOrWhiteSpace(query.Stage))
        {
            if (!EnumParsing.TryParse<LeadStage>(query.Stage, out var parsed))
            {
                return Error.Validation("Lead.InvalidStage", "Unknown pipeline stage.");
            }

            stage = parsed;
        }

        var actor = await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        Guid? ownerId = null;
        var unassigned = false;
        switch (query.Owner?.Trim())
        {
            case null or "":
                break;
            case "me":
                ownerId = actor.UserId;
                break;
            case "unassigned":
                unassigned = true;
                break;
            default:
                if (!Guid.TryParse(query.Owner, out var parsedOwner))
                {
                    return Error.Validation("Lead.InvalidOwnerFilter", "Owner is me, unassigned or a user id.");
                }

                ownerId = parsedOwner;
                break;
        }

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        if (search is { Length: > 100 })
        {
            return Error.Validation("Lead.InvalidSearch", "Search terms are limited to 100 characters.");
        }

        return new LeadFilter(stage, ownerId, unassigned, search, ScopeFor(actor), Math.Max(query.Page, 1), Math.Clamp(query.PageSize, 1, MaxPageSize));
    }

    private static LeadOwnerScope? ScopeFor(ActorPermissions actor) =>
        actor.IsSuperAdmin || actor.Permissions.Contains(Permissions.Leads.ViewAll) ? null : new LeadOwnerScope(actor.UserId);

    private async Task<Lead?> GetVisibleAsync(Guid id, CancellationToken cancellationToken) =>
        await GetVisibleAsync(id, await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    private async Task<Lead?> GetVisibleAsync(Guid id, ActorPermissions actor, CancellationToken cancellationToken)
    {
        var lead = await leads.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return lead is not null && (ScopeFor(actor) is not { } scope || lead.OwnerId is null || lead.OwnerId == scope.UserId) ? lead : null;
    }

    private Task<IReadOnlyDictionary<Guid, string>> OwnerNamesAsync(IEnumerable<Lead> source, CancellationToken cancellationToken) =>
        users.GetDisplayNamesAsync(source.Where(lead => lead.OwnerId is not null).Select(lead => lead.OwnerId!.Value).Distinct().ToList(), cancellationToken);
}
