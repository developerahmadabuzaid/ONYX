using Onyx.Domain.Products;

namespace Onyx.Application.Products;

public interface IAiDemoRunRepository
{
    void Add(AiDemoRun run);

    /// <summary>Claims up to <paramref name="max"/> queued runs for this Worker instance (row locks skip runs claimed elsewhere).</summary>
    Task<IReadOnlyList<AiDemoRun>> ClaimQueuedAsync(int max, CancellationToken cancellationToken);
}
