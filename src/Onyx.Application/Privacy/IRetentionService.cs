using Onyx.Contracts.Privacy;
using Onyx.Domain.Common;

namespace Onyx.Application.Privacy;

public interface IRetentionService
{
    /// <summary>Every category, saved or not (unsaved categories show as not configured).</summary>
    Task<IReadOnlyList<RetentionPolicyResponse>> ListAsync(CancellationToken cancellationToken);

    Task<Result<RetentionPolicyResponse>> SaveAsync(string category, SaveRetentionPolicyRequest request, CancellationToken cancellationToken);

    /// <summary>How many records the given settings would process now. Changes nothing.</summary>
    Task<Result<RetentionPreviewResponse>> PreviewAsync(string category, string action, int periodDays, CancellationToken cancellationToken);

    /// <summary>Applies every active policy in bounded batches (Worker). Categories without an active policy are untouched.</summary>
    Task<IReadOnlyList<RetentionRunSummary>> EnforceAsync(CancellationToken cancellationToken);
}
