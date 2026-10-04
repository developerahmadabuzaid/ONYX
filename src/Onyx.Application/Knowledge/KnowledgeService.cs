using Onyx.Application.Abstractions;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Content;
using Onyx.Application.Localization;
using Onyx.Contracts.Knowledge;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Knowledge;

namespace Onyx.Application.Knowledge;

/// <summary>Approved Company Knowledge: proposing (Knowledge.Edit) and verifying (Knowledge.Approve) are separate permissions.</summary>
public sealed class KnowledgeService(
    IKnowledgeRepository knowledge,
    IInformationRequestRepository requests,
    ICurrentUser currentUser,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IKnowledgeService
{
    private static readonly Error NotFound = Error.NotFound("Knowledge.NotFound", "Knowledge entry not found.");

    public async Task<Result<IReadOnlyList<KnowledgeEntryResponse>>> ListAsync(string? category, string? status, string? search, CancellationToken cancellationToken)
    {
        KnowledgeCategory? categoryFilter = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!EnumParsing.TryParse<KnowledgeCategory>(category, out var parsed))
            {
                return Error.Validation("Knowledge.InvalidCategory", "Unknown category.");
            }

            categoryFilter = parsed;
        }

        KnowledgeStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!EnumParsing.TryParse<KnowledgeStatus>(status, out var parsed))
            {
                return Error.Validation("Knowledge.InvalidStatus", "Unknown status.");
            }

            statusFilter = parsed;
        }

        var entries = await knowledge.ListAsync(categoryFilter, statusFilter, search, cancellationToken).ConfigureAwait(false);
        return entries.Select(ToResponse).ToList();
    }

    public async Task<Result<KnowledgeEntryResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await knowledge.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return entry is null ? NotFound : ToResponse(entry);
    }

    public async Task<Result<KnowledgeEntryResponse>> CreateAsync(SaveKnowledgeEntryRequest request, CancellationToken cancellationToken)
    {
        var parsed = Parse(request);
        if (parsed.IsFailure)
        {
            return parsed.Error!;
        }

        var created = KnowledgeEntry.Create(parsed.Value, request.Title, ContentMapping.ToDomain(request.Statements), request.SourceNote);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        knowledge.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(created.Value);
    }

    public async Task<Result<KnowledgeEntryResponse>> UpdateAsync(Guid id, SaveKnowledgeEntryRequest request, CancellationToken cancellationToken)
    {
        var parsed = Parse(request);
        if (parsed.IsFailure)
        {
            return parsed.Error!;
        }

        return await ChangeAsync(id, request.Version, entry => entry.Update(parsed.Value, request.Title, ContentMapping.ToDomain(request.Statements), request.SourceNote),
            null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<KnowledgeEntryResponse>> ApproveAsync(Guid id, KnowledgeVersionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = currentUser.UserId ?? throw new InvalidOperationException("An authenticated admin is required.");
        return await ChangeAsync(id, request.Version, entry => entry.Approve(userId, timeProvider.GetUtcNow()), AuditAction.KnowledgeApproved, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<KnowledgeEntryResponse>> RetireAsync(Guid id, KnowledgeVersionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await ChangeAsync(id, request.Version, entry => entry.Retire(), AuditAction.KnowledgeRetired, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<InformationRequestResponse>>> ListRequestsAsync(string? status, CancellationToken cancellationToken)
    {
        InformationRequestStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!EnumParsing.TryParse<InformationRequestStatus>(status, out var parsed))
            {
                return Error.Validation("InformationRequest.InvalidStatus", "Unknown status.");
            }

            filter = parsed;
        }

        return (await requests.ListAsync(filter, cancellationToken).ConfigureAwait(false)).Select(ToResponse).ToList();
    }

    public async Task<Result<InformationRequestResponse>> RaiseRequestAsync(RaiseInformationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var raised = InformationRequest.Raise(request.Question, request.Context, request.ContentItemId);
        if (raised.IsFailure)
        {
            return raised.Error!;
        }

        requests.Add(raised.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(raised.Value);
    }

    public async Task<Result<InformationRequestResponse>> ResolveRequestAsync(Guid id, ResolveInformationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<InformationRequestStatus>(request.Outcome, out var outcome))
        {
            return Error.Validation("InformationRequest.InvalidResolution", "Unknown outcome.");
        }

        var information = await requests.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (information is null)
        {
            return Error.NotFound("InformationRequest.NotFound", "Information request not found.");
        }

        if (outcome == InformationRequestStatus.Answered
            && (request.KnowledgeEntryId is not { } entryId || await knowledge.GetAsync(entryId, cancellationToken).ConfigureAwait(false) is not { Status: KnowledgeStatus.Approved }))
        {
            return Error.Validation("InformationRequest.NeedsApprovedKnowledge", "Answer with an approved knowledge entry.");
        }

        var resolved = information.Resolve(outcome, request.KnowledgeEntryId, request.Note, timeProvider.GetUtcNow());
        if (resolved.IsFailure)
        {
            return resolved.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(information);
    }

    public GovernanceRulesResponse GetGovernanceRules() => new(ContentGovernanceRules.All);

    private async Task<Result<KnowledgeEntryResponse>> ChangeAsync(
        Guid id, uint? version, Func<KnowledgeEntry, Result> change, AuditAction? action, CancellationToken cancellationToken)
    {
        var entry = await knowledge.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return NotFound;
        }

        if (version is { } expected)
        {
            knowledge.SetExpectedVersion(entry, expected);
        }

        var result = change(entry);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        if (action is { } audited)
        {
            auditTrail.Record(audited, AuditCategory.Content, nameof(KnowledgeEntry), entry.Id.ToString(), new { entry.Category, entry.Title });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(entry);
    }

    private static Result<KnowledgeCategory> Parse(SaveKnowledgeEntryRequest? request)
    {
        if (request is null || !EnumParsing.TryParse<KnowledgeCategory>(request.Category, out var category))
        {
            return Error.Validation("Knowledge.InvalidCategory", "Unknown category.");
        }

        return (request.Statements ?? []).Any(statement => !SupportedLanguages.All.Contains(statement.LanguageCode))
            ? Error.Validation("Knowledge.UnsupportedLanguage", "Statements must use configured languages.")
            : category;
    }

    private static KnowledgeEntryResponse ToResponse(KnowledgeEntry entry) => new(
        entry.Id, entry.Category.ToString(), entry.Title, entry.Statements.Select(ContentMapping.ToDto).ToList(), entry.SourceNote,
        entry.Status.ToString(), entry.VerifiedAtUtc, entry.VerifiedById, entry.UpdatedAtUtc ?? entry.CreatedAtUtc, entry.Version);

    private static InformationRequestResponse ToResponse(InformationRequest request) => new(
        request.Id, request.Question, request.Context, request.ContentItemId, request.Status.ToString(), request.KnowledgeEntryId,
        request.ResolutionNote, request.CreatedAtUtc, request.ResolvedAtUtc);
}
