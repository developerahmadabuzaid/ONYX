using Onyx.Domain.Intelligence;

namespace Onyx.Application.Intelligence;

/// <summary>Read side of provider-written performance snapshots (written only by future provider sync jobs).</summary>
public interface IContentPerformanceRepository
{
    Task<IReadOnlyList<ContentPerformanceSnapshot>> ListAsync(Guid contentItemId, string languageCode, int take, CancellationToken cancellationToken);

    Task<bool> AnyAsync(CancellationToken cancellationToken);
}
