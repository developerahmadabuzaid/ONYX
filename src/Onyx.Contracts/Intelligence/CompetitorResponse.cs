using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

public sealed record CompetitorResponse(
    Guid Id,
    string Name,
    string Domain,
    IReadOnlyList<string> Markets,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Topics,
    string? Notes,
    bool IsActive,
    uint Version);
