using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Common;
using Onyx.Application.Content;
using Onyx.Application.Localization;
using Onyx.Application.Providers;
using Onyx.Contracts.Common;
using Onyx.Contracts.Intelligence;
using Onyx.Domain.Common;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Providers;

namespace Onyx.Application.Intelligence;

/// <summary>The Keyword &amp; Content Map. Stores targeting decisions only; never volume, difficulty or rankings.</summary>
public sealed class KeywordMapService(
    IKeywordTargetRepository keywords,
    IContentRepository content,
    IProviderConnectionRepository connections,
    IUnitOfWork unitOfWork) : IKeywordMapService
{
    public const int MaxPageSize = 100;

    private static readonly Error NotFound = Error.NotFound("Keyword.NotFound", "Keyword not found.");

    public async Task<Result<PagedResult<KeywordTargetResponse>>> ListAsync(
        string? languageCode, string? status, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        KeywordStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!EnumParsing.TryParse<KeywordStatus>(status, out var parsed))
            {
                return Error.Validation("Keyword.InvalidStatus", "Unknown status.");
            }

            filter = parsed;
        }

        var dataStatus = await PerformanceStatusAsync(cancellationToken).ConfigureAwait(false);
        var result = await keywords.ListAsync(languageCode, filter, search, Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), cancellationToken).ConfigureAwait(false);
        return new PagedResult<KeywordTargetResponse>(result.Items.Select(target => ToResponse(target, dataStatus)).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<Result<KeywordTargetResponse>> CreateAsync(SaveKeywordTargetRequest request, CancellationToken cancellationToken)
    {
        var parsed = Parse(request);
        if (parsed.IsFailure)
        {
            return parsed.Error!;
        }

        var (intent, status) = parsed.Value;
        var created = KeywordTarget.Create(request.Keyword, request.LanguageCode, request.TargetMarket?.Trim().ToUpperInvariant(), intent);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var target = created.Value;
        if (await keywords.ExistsAsync(target.LanguageCode, target.NormalizedKeyword, target.TargetMarket, null, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Keyword.Exists", "This keyword is already in the map for this language and market. Update that row instead of creating a competing one.");
        }

        if (await ValidateTargetAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var updated = target.Update(intent, request.TargetContentItemId, request.ClusterId, status, request.Notes);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        keywords.Add(target);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(target, await PerformanceStatusAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<KeywordTargetResponse>> UpdateAsync(Guid id, SaveKeywordTargetRequest request, CancellationToken cancellationToken)
    {
        var parsed = Parse(request);
        if (parsed.IsFailure)
        {
            return parsed.Error!;
        }

        var target = await keywords.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return NotFound;
        }

        if (request.Version is { } version)
        {
            keywords.SetExpectedVersion(target, version);
        }

        if (await ValidateTargetAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var updated = target.Update(parsed.Value.Intent, request.TargetContentItemId, request.ClusterId, parsed.Value.Status, request.Notes);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(target, await PerformanceStatusAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var target = await keywords.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return NotFound;
        }

        keywords.Remove(target);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<KeywordConflictResponse>> CheckConflictsAsync(string languageCode, string keyword, Guid? excludeContentItemId, CancellationToken cancellationToken)
    {
        if (!SupportedLanguages.All.Contains(languageCode))
        {
            return Error.Validation("Keyword.UnsupportedLanguage", "Unsupported language.");
        }

        var normalized = TextNormalization.NormalizeKeyword(keyword);
        if (normalized.Length == 0)
        {
            return Error.Validation("Keyword.Invalid", "Enter a keyword.");
        }

        var conflicts = new List<KeywordConflictItem>();
        foreach (var target in await keywords.FindAsync(languageCode, normalized, cancellationToken).ConfigureAwait(false))
        {
            if (target.Status != KeywordStatus.Retired && target.TargetContentItemId != excludeContentItemId)
            {
                conflicts.Add(new KeywordConflictItem("KeywordMap", target.TargetContentItemId, target.Keyword, target.LanguageCode, target.SearchIntent.ToString(),
                    target.TargetMarket.Length == 0 ? null : target.TargetMarket));
            }
        }

        foreach (var translation in await content.FindByPrimaryKeywordAsync(languageCode, normalized, excludeContentItemId ?? Guid.Empty, cancellationToken).ConfigureAwait(false))
        {
            conflicts.Add(new KeywordConflictItem("SeoProfile", translation.ContentItemId, translation.PrimaryKeyword ?? keyword, languageCode,
                translation.SearchIntent.ToString(), translation.TargetMarket));
        }

        return new KeywordConflictResponse(conflicts.Count > 0, conflicts);
    }

    private async Task<Error?> ValidateTargetAsync(SaveKeywordTargetRequest request, CancellationToken cancellationToken) =>
        request.TargetContentItemId is { } targetId && !(await content.GetLookupAsync([targetId], cancellationToken).ConfigureAwait(false)).ContainsKey(targetId)
            ? Error.Validation("Keyword.InvalidTarget", "The target content does not exist.")
            : null;

    private async Task<string> PerformanceStatusAsync(CancellationToken cancellationToken)
    {
        var connection = await connections.GetByKeyAsync(ProviderKeys.SearchConsole, cancellationToken).ConfigureAwait(false);
        return connection is { Status: ProviderConnectionStatus.Active } ? "Connected" : "NotConnected";
    }

    private static Result<(SearchIntent Intent, KeywordStatus Status)> Parse(SaveKeywordTargetRequest? request)
    {
        if (request is null || !SupportedLanguages.All.Contains(request.LanguageCode))
        {
            return Error.Validation("Keyword.UnsupportedLanguage", "Unsupported language.");
        }

        if (!EnumParsing.TryParse<SearchIntent>(request.SearchIntent, out var intent) || !EnumParsing.TryParse<KeywordStatus>(request.Status, out var status))
        {
            return Error.Validation("Keyword.Invalid", "Unknown search intent or status.");
        }

        return (intent, status);
    }

    private static KeywordTargetResponse ToResponse(KeywordTarget target, string dataStatus) => new(
        target.Id, target.Keyword, target.LanguageCode, target.TargetMarket.Length == 0 ? null : target.TargetMarket, target.SearchIntent.ToString(),
        target.TargetContentItemId, target.ClusterId, target.Status.ToString(), target.Notes, dataStatus, target.Version);
}
