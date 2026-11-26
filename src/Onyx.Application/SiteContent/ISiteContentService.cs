using Onyx.Contracts.SiteContent;
using Onyx.Domain.Common;

namespace Onyx.Application.SiteContent;

/// <summary>
/// Editor overrides for the code-owned public site (homepage, navigation and footer, settings, site pages): draft, publish,
/// discard, revisions and restore, plus the published set the public site reads. Every write is validated and audited.
/// </summary>
public interface ISiteContentService
{
    Task<IReadOnlyList<SiteContentDocumentSummaryResponse>> ListAsync(CancellationToken cancellationToken);

    Task<Result<SiteContentDocumentResponse>> GetAsync(string key, CancellationToken cancellationToken);

    Task<Result<SiteContentDocumentResponse>> SaveDraftAsync(string key, SaveSiteContentDraftRequest request, CancellationToken cancellationToken);

    Task<Result<SiteContentDocumentResponse>> PublishAsync(string key, SiteContentVersionRequest request, CancellationToken cancellationToken);

    Task<Result<SiteContentDocumentResponse>> DiscardDraftAsync(string key, SiteContentVersionRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<SiteContentRevisionResponse>>> ListRevisionsAsync(string key, CancellationToken cancellationToken);

    Task<Result<SiteContentDocumentResponse>> RestoreRevisionAsync(string key, int number, SiteContentVersionRequest request, CancellationToken cancellationToken);

    Task<PublicSiteContentResponse> GetPublishedAsync(CancellationToken cancellationToken);
}
