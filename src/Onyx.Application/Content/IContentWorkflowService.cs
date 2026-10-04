using Onyx.Contracts.Content;
using Onyx.Domain.Common;

namespace Onyx.Application.Content;

public interface IContentWorkflowService
{
    Task<Result<ContentDetailResponse>> SubmitForReviewAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> ApproveAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> RejectAsync(Guid id, RejectContentRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> PublishAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> ScheduleAsync(Guid id, ScheduleContentRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> CancelScheduleAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> ScheduleUnpublishAsync(Guid id, ScheduleUnpublishRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> CancelUnpublishScheduleAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> UnpublishAsync(Guid id, TakeOfflineRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> ArchiveAsync(Guid id, TakeOfflineRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> RestoreFromArchiveAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> ResolveOfflineAsync(Guid id, TakeOfflineRequest request, CancellationToken cancellationToken);

    Task<Result<QualityReportResponse>> GetQualityReportAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<PublishingTimeRecommendationResponse>> RecommendPublishingTimeAsync(Guid id, CancellationToken cancellationToken);
}
