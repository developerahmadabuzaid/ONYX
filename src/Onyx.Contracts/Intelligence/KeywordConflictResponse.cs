using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

public sealed record KeywordConflictResponse(bool HasConflict, IReadOnlyList<KeywordConflictItem> Conflicts);
