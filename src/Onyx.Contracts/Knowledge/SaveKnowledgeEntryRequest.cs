using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Knowledge;

public sealed record SaveKnowledgeEntryRequest(string Category, string Title, IReadOnlyList<LocalizedTextDto> Statements, string? SourceNote, uint? Version);
