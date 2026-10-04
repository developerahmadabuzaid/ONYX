using Onyx.Application.Abstractions;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Localization;
using Onyx.Application.Media;
using Onyx.Contracts.Content;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>
/// Secure draft previews: a random, hashed, expiring token per language. The preview renders the working copy with the same
/// builder as the live site plus an SEO preview, and is served noindex/no-store by the site.
/// </summary>
public sealed class ContentPreviewService(
    IContentRepository content,
    IContentPreviewTokenRepository tokens,
    IMediaRepository media,
    ContentAccess access,
    ContentPathService pathService,
    ContentQualityGate qualityGate,
    PublicContentBuilder builder,
    ContentSettings settings,
    ICurrentUser currentUser,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IContentPreviewService
{
    private static readonly Error InvalidPreview = Error.NotFound("Preview.Invalid", "This preview link is invalid or has expired.");

    public async Task<Result<PreviewLinkResponse>> CreateLinkAsync(Guid contentItemId, CreatePreviewLinkRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SupportedLanguages.All.Contains(request.LanguageCode))
        {
            return Error.Validation("Content.UnsupportedLanguage", "Unsupported language.");
        }

        var loaded = await access.ForReadAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        if (loaded.Value.Item.GetTranslation(request.LanguageCode) is null)
        {
            return Error.Conflict("Preview.NoTranslation", "Write this language before previewing it.");
        }

        var token = PreviewTokenCodec.NewToken();
        var issued = ContentPreviewToken.Issue(contentItemId, request.LanguageCode, PreviewTokenCodec.Hash(token), timeProvider.GetUtcNow(),
            settings.PreviewTokenLifetime, currentUser.UserId);
        tokens.Add(issued);
        auditTrail.Record(AuditAction.ContentPreviewLinkCreated, AuditCategory.Content, nameof(ContentItem), contentItemId.ToString(),
            new { language = request.LanguageCode, expiresAtUtc = issued.ExpiresAtUtc });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new PreviewLinkResponse(token, request.LanguageCode, issued.ExpiresAtUtc);
    }

    public async Task<Result<ContentPreviewResponse>> GetPreviewAsync(string token, CancellationToken cancellationToken)
    {
        var grant = await ValidAsync(token, cancellationToken).ConfigureAwait(false);
        if (grant is null)
        {
            return InvalidPreview;
        }

        var items = await content.GetLookupAsync([grant.ContentItemId], cancellationToken).ConfigureAwait(false);
        if (!items.TryGetValue(grant.ContentItemId, out var item) || item.GetTranslation(grant.LanguageCode) is null)
        {
            return InvalidPreview;
        }

        var snapshot = ContentSnapshot.From(item);
        var computed = await pathService.ComputeAsync(item, snapshot, cancellationToken).ConfigureAwait(false);
        var wouldBePath = computed.IsSuccess && computed.Value.TryGetValue(grant.LanguageCode, out var path) ? $"/{grant.LanguageCode}/{path}" : null;
        var built = await builder.BuildAsync(item, snapshot, grant.LanguageCode, wouldBePath, cancellationToken).ConfigureAwait(false);

        // A draft is never indexable, whatever its own SEO settings say.
        built = built with { Seo = built.Seo with { Index = false, Follow = false } };
        var report = await qualityGate.EvaluateAsync(item, cancellationToken).ConfigureAwait(false);
        return new ContentPreviewResponse(built, await SeoPreviewAsync(snapshot, grant.LanguageCode, wouldBePath, built, report, cancellationToken).ConfigureAwait(false), grant.ExpiresAtUtc);
    }

    public async Task<bool> CanViewMediaAsync(string? token, Guid mediaAssetId, CancellationToken cancellationToken)
    {
        var grant = await ValidAsync(token, cancellationToken).ConfigureAwait(false);
        return grant is not null && await media.IsUsedByWorkingCopyAsync(mediaAssetId, grant.ContentItemId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ContentPreviewToken?> ValidAsync(string? token, CancellationToken cancellationToken)
    {
        if (!PreviewTokenCodec.IsWellFormed(token))
        {
            return null;
        }

        var grant = await tokens.FindByHashAsync(PreviewTokenCodec.Hash(token!), cancellationToken).ConfigureAwait(false);
        return grant is not null && grant.IsValidAt(timeProvider.GetUtcNow()) ? grant : null;
    }

    private async Task<SeoPreviewResponse> SeoPreviewAsync(
        ContentSnapshot snapshot, string languageCode, string? path, PublicContentResponse built, QualityReport report, CancellationToken cancellationToken)
    {
        var translation = snapshot.Translation(languageCode)!;
        var headings = new List<HeadingOutlineResponse> { new(1, translation.Title) };
        headings.AddRange(translation.Body
            .Where(block => block.Type == ContentBlockType.Heading)
            .Select(block => new HeadingOutlineResponse(block.Level ?? 2, string.Concat((block.Spans ?? []).Select(span => span.Text)))));

        var references = MediaUsageIndexer.References(snapshot).Where(reference => reference.LanguageCode == languageCode).ToList();
        var assets = await media.GetLookupAsync(references.Select(reference => reference.MediaAssetId).Distinct().ToList(), cancellationToken).ConfigureAwait(false);
        var images = references.Select(reference =>
        {
            var exists = assets.TryGetValue(reference.MediaAssetId, out var asset);
            var contextAlt = translation.Body.FirstOrDefault(block => block.MediaAssetId == reference.MediaAssetId && block.Alt is not null)?.Alt;
            var alt = contextAlt ?? asset?.DefaultAlt(languageCode);
            return new ImageCheckResponse(reference.MediaAssetId, exists, alt is not null, alt, asset?.Width, asset?.Height, reference.Context.ToString());
        }).ToList();

        return new SeoPreviewResponse(
            path ?? "(available once the parent page is live)",
            built.Seo.Title,
            built.Seo.Description,
            $"{(translation.Seo.Index ? "index" : "noindex")}, {(translation.Seo.Follow ? "follow" : "nofollow")}",
            translation.Seo.CanonicalPath ?? path,
            headings,
            images,
            translation.Faqs.Count,
            built.Seo.StructuredData,
            report.ToResponse());
    }
}
