using System.Text;
using Onyx.Application.Auditing;
using Onyx.Contracts.Content;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>
/// Public URL paths of live content. Paths derive from the live slugs: section segment + ancestors + slug. When a path changes,
/// the old one stays recorded (and permanently redirects to the new one); live descendants are updated in the same step, and
/// every redirect resolves straight to the current URL, so there are no chains.
/// </summary>
public sealed class ContentPathService(
    IContentRepository content,
    IContentPathRepository paths,
    IContentRevisionRepository revisions,
    IRedirectRepository redirects,
    IAuditTrail auditTrail,
    TimeProvider timeProvider)
{
    private const int MaxDepth = 10;

    public static readonly Error ParentNotLive =
        Error.Conflict("Content.ParentNotLive", "Publish the parent first: a child page's URL is built from its parent's live URL.");

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var segments = path.Normalize(NormalizationForm.FormC).Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join('/', segments);
    }

    /// <summary>The path each language of <paramref name="snapshot"/> would have if it were live now.</summary>
    public async Task<Result<IReadOnlyDictionary<string, string>>> ComputeAsync(ContentItem item, ContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(snapshot);

        IReadOnlyDictionary<(Guid, string), string> parentPaths = new Dictionary<(Guid, string), string>();
        if (snapshot.ParentId is { } parentId)
        {
            var parents = await content.GetLookupAsync([parentId], cancellationToken).ConfigureAwait(false);
            if (!parents.TryGetValue(parentId, out var parent) || !parent.IsLive)
            {
                return ParentNotLive;
            }

            parentPaths = await paths.GetCurrentPathsAsync([parentId], cancellationToken).ConfigureAwait(false);
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var translation in snapshot.Translations)
        {
            var language = translation.LanguageCode;
            string? prefix = snapshot.Type switch
            {
                ContentType.ServicePillar => ContentRouteSegments.Services.GetValueOrDefault(language),
                ContentType.Industry => ContentRouteSegments.Industries.GetValueOrDefault(language),
                _ => snapshot.ParentId is { } parent ? parentPaths.GetValueOrDefault((parent, language)) : null,
            };

            if (snapshot.Type is ContentType.ServicePillar or ContentType.Industry && prefix is null
                || snapshot.ParentId is not null && prefix is null)
            {
                return Error.Validation("Content.NoRoute", $"No URL section is configured for '{language}', or the parent has no URL in that language.");
            }

            if (prefix is null && ContentRouteSegments.Reserved.Contains(translation.Slug))
            {
                return Error.Validation("Content.ReservedSlug", $"'{translation.Slug}' is reserved by the site and cannot be a top-level page.");
            }

            var path = NormalizePath(prefix is null ? translation.Slug : $"{prefix}/{translation.Slug}");
            if (path.Length is 0 or > ContentPath.PathMaxLength)
            {
                return Error.Validation("Content.PathTooLong", "The resulting URL is too long.");
            }

            result[language] = path;
        }

        return result;
    }

    /// <summary>Makes <paramref name="newPaths"/> current for the item; previous paths become permanent redirects.</summary>
    /// <param name="changedUrls">Receives every site path (with language prefix) whose answer changed: the old URL and the new one.</param>
    public async Task<Result> ApplyAsync(
        Guid contentItemId, IReadOnlyDictionary<string, string> newPaths, ISet<string>? changedUrls, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newPaths);
        var existing = await paths.ListForItemAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();

        foreach (var (language, newPath) in newPaths)
        {
            var current = existing.FirstOrDefault(path => path.LanguageCode == language && path.IsCurrent);
            if (current?.Path == newPath)
            {
                continue;
            }

            var owner = await paths.FindAsync(language, newPath, cancellationToken).ConfigureAwait(false);
            if (owner is not null && owner.ContentItemId != contentItemId)
            {
                return Error.Conflict("Content.PathTaken", $"The URL '/{language}/{newPath}' belongs to other content (now or in its history).");
            }

            if (await redirects.FromPathExistsAsync(language, newPath, null, cancellationToken).ConfigureAwait(false))
            {
                return Error.Conflict("Content.PathHasRedirect", $"A redirect rule exists for '/{language}/{newPath}'. Remove it before using this URL.");
            }

            if (current is not null)
            {
                changedUrls?.Add($"/{language}/{current.Path}");
            }

            changedUrls?.Add($"/{language}/{newPath}");
            current?.Retire(now);
            var reused = existing.FirstOrDefault(path => path.LanguageCode == language && path.Path == newPath);
            if (reused is not null)
            {
                reused.Reactivate();
            }
            else
            {
                paths.Add(ContentPath.Create(contentItemId, language, newPath, now));
            }

            if (current is not null)
            {
                auditTrail.Record(AuditAction.ContentSlugChanged, AuditCategory.Publishing, nameof(ContentItem), contentItemId.ToString(),
                    new { language, from = current.Path, to = newPath });
            }
        }

        return Result.Success();
    }

    /// <summary>After a parent's URL changed, moves its live descendants under the new URL (their old URLs keep redirecting).</summary>
    public Task<Result> RefreshDescendantsAsync(Guid parentId, ISet<string>? changedUrls, CancellationToken cancellationToken) =>
        RefreshAsync(parentId, 0, changedUrls, cancellationToken);

    private async Task<Result> RefreshAsync(Guid parentId, int depth, ISet<string>? changedUrls, CancellationToken cancellationToken)
    {
        if (depth >= MaxDepth)
        {
            return Result.Success();
        }

        var children = (await content.ListChildrenAsync(parentId, cancellationToken).ConfigureAwait(false)).Where(child => child.IsLive).ToList();
        if (children.Count == 0)
        {
            return Result.Success();
        }

        var liveRevisions = await revisions.GetManyAsync(children.Select(child => child.LiveRevisionId!.Value).ToList(), cancellationToken).ConfigureAwait(false);
        foreach (var child in children)
        {
            if (!liveRevisions.TryGetValue(child.LiveRevisionId!.Value, out var revision))
            {
                continue;
            }

            var computed = await ComputeAsync(child, ContentSnapshotSerializer.Deserialize(revision.Snapshot), cancellationToken).ConfigureAwait(false);
            if (computed.IsFailure)
            {
                return computed.Error!;
            }

            var applied = await ApplyAsync(child.Id, computed.Value, changedUrls, cancellationToken).ConfigureAwait(false);
            if (applied.IsFailure)
            {
                return applied;
            }

            var nested = await RefreshAsync(child.Id, depth + 1, changedUrls, cancellationToken).ConfigureAwait(false);
            if (nested.IsFailure)
            {
                return nested;
            }
        }

        return Result.Success();
    }
}
