using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Media;

/// <summary>Per language: <see cref="LocalizedTextDto.Text"/> is the default alt text, <see cref="LocalizedTextDto.Secondary"/> the caption.</summary>
public sealed record UpdateMediaTextsRequest(string? Title, IReadOnlyList<LocalizedTextDto> Texts, uint Version);
