using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record RevisionComparisonResponse(int FromNumber, int ToNumber, IReadOnlyList<FieldDifferenceResponse> Differences);
