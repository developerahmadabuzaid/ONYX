using Onyx.Domain.Intelligence;

namespace Onyx.Application.Intelligence;

/// <summary>Provider-written performance snapshots (written only by the analytics sync jobs, Stage 12).</summary>
public interface IContentPerformanceRepository
{
    Task<IReadOnlyList<ContentPerformanceSnapshot>> ListAsync(Guid contentItemId, string languageCode, int take, CancellationToken cancellationToken);

    Task<bool> AnyAsync(CancellationToken cancellationToken);

    /// <summary>Replaces the source's snapshots for the period starting at <paramref name="periodStartUtc"/> (idempotent; caller commits).</summary>
    Task ReplacePeriodAsync(EvidenceSource source, DateTimeOffset periodStartUtc, IReadOnlyList<ContentPerformanceSnapshot> snapshots, CancellationToken cancellationToken);
}
