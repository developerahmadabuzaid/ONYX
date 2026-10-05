using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Contracts.Insights;

public sealed record AuthorResponse(Guid Id, IReadOnlyList<LocalizedTextDto> Names, string ConsentNote, bool IsActive, uint Version);
