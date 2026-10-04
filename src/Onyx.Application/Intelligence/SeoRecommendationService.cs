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
/// Recommendation review. Editors may record their own recommendations (as editor judgement, without provider metrics);
/// rule-based and future AI recommendations arrive with their evidence. Nothing changes published content directly.
/// </summary>
public sealed class SeoRecommendationService(
    ISeoRecommendationRepository recommendations,
    IContentRepository content,
    ContentAccess access,
    ContentRevisionRecorder revisionRecorder,
    ICurrentUser currentUser,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ISeoRecommendationService
{
    public const int MaxPageSize = 100;

    public static readonly IReadOnlySet<string> ApplicableFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "title", "slug", "metaTitle", "metaDescription", "primaryKeyword", "secondaryKeywords", "searchIntent",
    };

    private static readonly Error NotFound = Error.NotFound("Recommendation.NotFound", "Recommendation not found.");

    public async Task<Result<PagedResult<RecommendationResponse>>> ListAsync(
        string? status, Guid? contentItemId, string? kind, int page, int pageSize, CancellationToken cancellationToken)
    {
        RecommendationStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!EnumParsing.TryParse<RecommendationStatus>(status, out var parsed))
            {
                return Error.Validation("Recommendation.InvalidStatus", "Unknown status.");
            }

            statusFilter = parsed;
        }

        RecommendationKind? kindFilter = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!EnumParsing.TryParse<RecommendationKind>(kind, out var parsed))
            {
                return Error.Validation("Recommendation.InvalidKind", "Unknown kind.");
            }

            kindFilter = parsed;
        }

        var result = await recommendations.ListAsync(statusFilter, contentItemId, kindFilter, Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), cancellationToken)
            .ConfigureAwait(false);
        var observations = await recommendations.ListObservationsAsync(result.Items.Select(item => item.Id).ToList(), cancellationToken).ConfigureAwait(false);
        return new PagedResult<RecommendationResponse>(
            result.Items.Select(item => IntelligenceMapping.ToResponse(item, observations.GetValueOrDefault(item.Id) ?? [])).ToList(),
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<Result<RecommendationResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var recommendation = await recommendations.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return recommendation is null ? NotFound : await ToResponseAsync(recommendation, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RecommendationResponse>> ProposeAsync(ProposeRecommendationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<RecommendationKind>(request.Kind, out var kind))
        {
            return Error.Validation("Recommendation.InvalidKind", "Unknown kind.");
        }

        if (request.LanguageCode is not null && !SupportedLanguages.All.Contains(request.LanguageCode))
        {
            return Error.Validation("Recommendation.UnsupportedLanguage", "Unsupported language.");
        }

        if (request.ContentItemId is { } contentId && !(await content.GetLookupAsync([contentId], cancellationToken).ConfigureAwait(false)).ContainsKey(contentId))
        {
            return ContentErrors.NotFound;
        }

        var proposed = SeoRecommendation.Propose(
            kind, RecommendationOrigin.Editor, RecommendationBasis.Manual, request.Title, request.DetectedIssue, request.Reasoning,
            (request.ProposedChanges ?? []).Select(change => new ProposedFieldChange(change.Field, change.CurrentValue, change.ProposedValue)).ToList(),
            [], request.ContentItemId, request.LanguageCode, request.ClusterId);
        if (proposed.IsFailure)
        {
            return proposed.Error!;
        }

        recommendations.Add(proposed.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return IntelligenceMapping.ToResponse(proposed.Value, []);
    }

    public Task<Result<RecommendationResponse>> ApproveAsync(Guid id, DecideRequest request, CancellationToken cancellationToken) =>
        DecideAsync(id, request, approve: true, cancellationToken);

    public Task<Result<RecommendationResponse>> RejectAsync(Guid id, DecideRequest request, CancellationToken cancellationToken) =>
        DecideAsync(id, request, approve: false, cancellationToken);

    public async Task<Result<RecommendationResponse>> ApplyAsync(Guid id, DecideRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var recommendation = await recommendations.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (recommendation is null)
        {
            return NotFound;
        }

        recommendations.SetExpectedVersion(recommendation, request.Version);
        if (recommendation.Status != RecommendationStatus.Approved)
        {
            return Error.Conflict("Recommendation.NotApproved", "Approve the recommendation before applying it.");
        }

        if (recommendation.ContentItemId is not { } contentId || recommendation.LanguageCode is not { } language || recommendation.ProposedChanges.Count == 0)
        {
            return Error.Conflict("Recommendation.ManualApply", "This recommendation has no single-field change to apply; apply it by hand and mark it applied.");
        }

        if (recommendation.ProposedChanges.FirstOrDefault(change => !ApplicableFields.Contains(change.Field)) is { } unsupported)
        {
            return Error.Conflict("Recommendation.ManualApply", $"'{unsupported.Field}' cannot be applied automatically; apply it by hand and mark it applied.");
        }

        var loaded = await access.ForReadAsync(contentId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (item, actor) = loaded.Value;
        if (ContentAuthorizer.CheckEdit(actor, item.Type) is { } forbidden)
        {
            return forbidden;
        }

        foreach (var change in recommendation.ProposedChanges)
        {
            var applied = await ApplyChangeAsync(item, language, change, cancellationToken).ConfigureAwait(false);
            if (applied.IsFailure)
            {
                return applied.Error!;
            }
        }

        var revision = await revisionRecorder.RecordAsync(item, RevisionKind.RecommendationApplied, $"Applied recommendation: {recommendation.Title}", null, cancellationToken)
            .ConfigureAwait(false);
        var marked = recommendation.MarkApplied(revision.Number, timeProvider.GetUtcNow());
        if (marked.IsFailure)
        {
            return marked.Error!;
        }

        auditTrail.Record(AuditAction.SeoRecommendationApplied, AuditCategory.Content, nameof(SeoRecommendation), recommendation.Id.ToString(),
            new { contentItemId = contentId, language, revision = revision.Number, fields = recommendation.ProposedChanges.Select(change => change.Field).ToList() });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToResponseAsync(recommendation, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RecommendationResponse>> MarkAppliedAsync(Guid id, DecideRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var recommendation = await recommendations.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (recommendation is null)
        {
            return NotFound;
        }

        recommendations.SetExpectedVersion(recommendation, request.Version);
        var marked = recommendation.MarkApplied(null, timeProvider.GetUtcNow());
        if (marked.IsFailure)
        {
            return marked.Error!;
        }

        auditTrail.Record(AuditAction.SeoRecommendationApplied, AuditCategory.Content, nameof(SeoRecommendation), recommendation.Id.ToString(),
            new { manual = true, note = request.Note });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToResponseAsync(recommendation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> ApplyChangeAsync(ContentItem item, string language, ProposedFieldChange change, CancellationToken cancellationToken)
    {
        var translation = item.GetTranslation(language);
        if (translation is null)
        {
            return Error.Conflict("Recommendation.NoTranslation", "The content has no working copy in this language.");
        }

        var value = change.ProposedValue?.Trim();
        var seo = translation.Seo;
        switch (change.Field)
        {
            case "title":
                return item.UpdateTranslation(language, value ?? string.Empty, translation.Summary, translation.Slug, translation.Body, translation.Faqs);
            case "slug":
                var slug = Slug.Normalize(value);
                if (await content.SlugTakenAsync(language, translation.SlugScope, slug, item.Id, cancellationToken).ConfigureAwait(false))
                {
                    return Error.Conflict("Content.SlugTaken", "Another item in the same section already uses this slug.");
                }

                return item.UpdateTranslation(language, translation.Title, translation.Summary, slug, translation.Body, translation.Faqs);
            case "metaTitle":
                return item.UpdateSeo(language, seo with { MetaTitle = value });
            case "metaDescription":
                return item.UpdateSeo(language, seo with { MetaDescription = value });
            case "primaryKeyword":
                return item.UpdateSeo(language, seo with { PrimaryKeyword = value });
            case "secondaryKeywords":
                return item.UpdateSeo(language, seo with { SecondaryKeywords = (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) });
            case "searchIntent":
                return EnumParsing.TryParse<SearchIntent>(value, out var intent)
                    ? item.UpdateSeo(language, seo with { SearchIntent = intent })
                    : Error.Validation("Recommendation.InvalidValue", "Unknown search intent.");
            default:
                return Error.Conflict("Recommendation.ManualApply", "This field cannot be applied automatically.");
        }
    }

    private async Task<Result<RecommendationResponse>> DecideAsync(Guid id, DecideRequest request, bool approve, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var recommendation = await recommendations.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (recommendation is null)
        {
            return NotFound;
        }

        recommendations.SetExpectedVersion(recommendation, request.Version);
        var userId = currentUser.UserId ?? throw new InvalidOperationException("An authenticated admin is required.");
        var decided = approve
            ? recommendation.Approve(userId, request.Note, timeProvider.GetUtcNow())
            : recommendation.Reject(userId, request.Note, timeProvider.GetUtcNow());
        if (decided.IsFailure)
        {
            return decided.Error!;
        }

        auditTrail.Record(approve ? AuditAction.SeoRecommendationApproved : AuditAction.SeoRecommendationRejected, AuditCategory.Content,
            nameof(SeoRecommendation), recommendation.Id.ToString(), new { recommendation.Kind, note = request.Note });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToResponseAsync(recommendation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RecommendationResponse> ToResponseAsync(SeoRecommendation recommendation, CancellationToken cancellationToken)
    {
        var observations = await recommendations.ListObservationsAsync([recommendation.Id], cancellationToken).ConfigureAwait(false);
        return IntelligenceMapping.ToResponse(recommendation, observations.GetValueOrDefault(recommendation.Id) ?? []);
    }
}
