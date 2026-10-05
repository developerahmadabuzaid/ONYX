using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Contracts.Insights;

/// <summary><see cref="Names"/>: Text = name, Secondary = job title, per language.</summary>
public sealed record SaveAuthorRequest(IReadOnlyList<LocalizedTextDto> Names, string ConsentNote, bool IsActive, uint? Version);
