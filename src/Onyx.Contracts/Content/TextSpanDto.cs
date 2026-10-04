using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary><see cref="Emphasis"/>: None, Strong or Emphasis.</summary>
public sealed record TextSpanDto(string Text, string? Emphasis = null, LinkTargetDto? Link = null);
