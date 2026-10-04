using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Localization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>
/// Editor-managed redirect rules. Paths owned by content (current or historical) are managed automatically and cannot be
/// overridden here; rules that would loop are refused; targets are internal only.
/// </summary>
public sealed class RedirectAdministrationService(
    IRedirectRepository redirects,
    IContentPathRepository paths,
    IContentRepository content,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork) : IRedirectAdministrationService
{
    public const int MaxPageSize = 100;
    private const int MaxChain = 6;

    public async Task<PagedResult<RedirectResponse>> ListAsync(string? languageCode, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var result = await redirects.ListAsync(languageCode, search, Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), cancellationToken).ConfigureAwait(false);
        return new PagedResult<RedirectResponse>(result.Items.Select(ToResponse).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<Result<RedirectResponse>> CreateAsync(SaveRedirectRequest request, CancellationToken cancellationToken)
    {
        var validated = await ValidateAsync(request, null, cancellationToken).ConfigureAwait(false);
        if (validated.IsFailure)
        {
            return validated.Error!;
        }

        var (from, kind, targetPath) = validated.Value;
        var created = Redirect.Create(request.LanguageCode, from, kind, request.TargetContentItemId, targetPath, request.Note);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        if (!request.IsActive)
        {
            created.Value.Update(kind, request.TargetContentItemId, targetPath, request.Note, isActive: false);
        }

        redirects.Add(created.Value);
        auditTrail.Record(AuditAction.RedirectCreated, AuditCategory.Publishing, nameof(Redirect), created.Value.Id.ToString(),
            new { language = request.LanguageCode, from, kind = kind.ToString(), targetContentItemId = request.TargetContentItemId, targetPath });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(created.Value);
    }

    public async Task<Result<RedirectResponse>> UpdateAsync(Guid id, SaveRedirectRequest request, CancellationToken cancellationToken)
    {
        var redirect = await redirects.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (redirect is null)
        {
            return Error.NotFound("Redirect.NotFound", "Redirect not found.");
        }

        if (request is null || request.LanguageCode != redirect.LanguageCode || ContentPathService.NormalizePath(request.FromPath) != redirect.FromPath)
        {
            return Error.Validation("Redirect.Immutable", "The language and source path of a redirect cannot change; create a new rule instead.");
        }

        var validated = await ValidateAsync(request, id, cancellationToken).ConfigureAwait(false);
        if (validated.IsFailure)
        {
            return validated.Error!;
        }

        var (_, kind, targetPath) = validated.Value;
        var updated = redirect.Update(kind, request.TargetContentItemId, targetPath, request.Note, request.IsActive);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        auditTrail.Record(AuditAction.RedirectChanged, AuditCategory.Publishing, nameof(Redirect), redirect.Id.ToString(),
            new { kind = kind.ToString(), targetContentItemId = request.TargetContentItemId, targetPath, request.IsActive });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(redirect);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var redirect = await redirects.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (redirect is null)
        {
            return Error.NotFound("Redirect.NotFound", "Redirect not found.");
        }

        redirects.Remove(redirect);
        auditTrail.Record(AuditAction.RedirectDeleted, AuditCategory.Publishing, nameof(Redirect), redirect.Id.ToString(),
            new { language = redirect.LanguageCode, from = redirect.FromPath, kind = redirect.Kind.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result<(string From, RedirectKind Kind, string? TargetPath)>> ValidateAsync(
        SaveRedirectRequest? request, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (request is null || !SupportedLanguages.All.Contains(request.LanguageCode))
        {
            return Error.Validation("Redirect.InvalidLanguage", "Unsupported language.");
        }

        if (!EnumParsing.TryParse<RedirectKind>(request.Kind, out var kind))
        {
            return Error.Validation("Redirect.InvalidKind", "Unknown redirect kind.");
        }

        var from = ContentPathService.NormalizePath(StripLanguage(request.FromPath, request.LanguageCode));
        if (!Redirect.IsSafePath(from))
        {
            return Error.Validation("Redirect.InvalidPath", "Enter the old path without the domain, e.g. old-page or services/old-name.");
        }

        if (excludeId is null && await redirects.FromPathExistsAsync(request.LanguageCode, from, excludeId, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Redirect.Exists", "A rule already exists for this path.");
        }

        if (await paths.FindAsync(request.LanguageCode, from, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Error.Conflict("Redirect.ManagedByContent", "This path belongs to content; its redirects are managed automatically.");
        }

        string? targetPath = null;
        if (!string.IsNullOrWhiteSpace(request.TargetPath))
        {
            // Checked before normalization, which would otherwise turn "//host" or "https://host" into an innocent-looking site path.
            var raw = request.TargetPath.Trim();
            targetPath = ContentPathService.NormalizePath(StripLanguage(raw, request.LanguageCode));
            if (raw.StartsWith("//", StringComparison.Ordinal) || raw.Contains(':', StringComparison.Ordinal) || raw.Contains('\\', StringComparison.Ordinal)
                || !Redirect.IsSafePath(targetPath))
            {
                return Error.Validation("Redirect.InvalidTarget", "Redirect targets are site paths in the same language (no external URLs).");
            }

            if (await LeadsBackAsync(request.LanguageCode, from, targetPath, cancellationToken).ConfigureAwait(false))
            {
                return Error.Validation("Redirect.Loop", "This redirect would create a loop.");
            }
        }

        if (request.TargetContentItemId is { } targetId && !(await content.GetLookupAsync([targetId], cancellationToken).ConfigureAwait(false)).ContainsKey(targetId))
        {
            return Error.Validation("Redirect.InvalidTarget", "The target content does not exist.");
        }

        return (from, kind, targetPath);
    }

    /// <summary>Follows existing rules from the target; refuses if they ever return to <paramref name="from"/>.</summary>
    private async Task<bool> LeadsBackAsync(string languageCode, string from, string target, CancellationToken cancellationToken)
    {
        var current = target;
        for (var hop = 0; hop < MaxChain; hop++)
        {
            if (current == from)
            {
                return true;
            }

            var next = await redirects.FindActiveAsync(languageCode, current, cancellationToken).ConfigureAwait(false);
            if (next?.TargetPath is null)
            {
                return false;
            }

            current = next.TargetPath;
        }

        return true;
    }

    private static string StripLanguage(string? path, string languageCode)
    {
        var trimmed = (path ?? string.Empty).Trim();
        var prefix = $"/{languageCode}/";
        return trimmed.StartsWith(prefix, StringComparison.Ordinal) ? trimmed[prefix.Length..] : trimmed;
    }

    private static RedirectResponse ToResponse(Redirect redirect) =>
        new(redirect.Id, redirect.LanguageCode, redirect.FromPath, redirect.Kind.ToString(), redirect.TargetContentItemId, redirect.TargetPath,
            redirect.Note, redirect.IsActive, redirect.CreatedAtUtc);
}
