using Onyx.Domain.Common;

namespace Onyx.Application.Content;

/// <summary>
/// Scheduled publishing and unpublishing, driven by the Worker. Each item is processed in its own unit of work (the caller
/// opens a new scope per item), so one failure never leaves another item half-changed.
/// </summary>
public interface IContentSchedulingService
{
    Task<IReadOnlyList<Guid>> ListDuePublicationsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListDueUnpublicationsAsync(CancellationToken cancellationToken);

    Task<Result> PublishDueAsync(Guid contentItemId, CancellationToken cancellationToken);

    Task<Result> UnpublishDueAsync(Guid contentItemId, CancellationToken cancellationToken);

    /// <summary>Returns a scheduled item to Approved and notifies editors why its publication did not happen.</summary>
    Task HandleFailedPublicationAsync(Guid contentItemId, string reason, CancellationToken cancellationToken);
}
