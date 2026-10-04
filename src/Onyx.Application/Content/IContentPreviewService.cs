using Onyx.Contracts.Content;
using Onyx.Domain.Common;

namespace Onyx.Application.Content;

public interface IContentPreviewService
{
    Task<Result<PreviewLinkResponse>> CreateLinkAsync(Guid contentItemId, CreatePreviewLinkRequest request, CancellationToken cancellationToken);

    /// <summary>Anonymous: the token is the only credential, read-only and short-lived.</summary>
    Task<Result<ContentPreviewResponse>> GetPreviewAsync(string token, CancellationToken cancellationToken);

    /// <summary>Whether a valid preview token's content uses the media asset in its working copy.</summary>
    Task<bool> CanViewMediaAsync(string? token, Guid mediaAssetId, CancellationToken cancellationToken);
}
