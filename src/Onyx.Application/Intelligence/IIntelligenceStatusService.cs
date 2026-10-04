using Onyx.Contracts.Intelligence;
using Onyx.Domain.Common;

namespace Onyx.Application.Intelligence;

public interface IIntelligenceStatusService
{
    /// <summary>Which evidence sources exist, and whether each is actually connected.</summary>
    Task<IntelligenceCapabilitiesResponse> GetCapabilitiesAsync(CancellationToken cancellationToken);

    /// <summary>Per-page intelligence profile from real provider snapshots only; otherwise "insufficient data".</summary>
    Task<Result<ContentIntelligenceResponse>> GetContentProfileAsync(Guid contentItemId, string languageCode, CancellationToken cancellationToken);
}
