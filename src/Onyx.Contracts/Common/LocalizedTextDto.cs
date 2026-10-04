using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Common;

/// <summary>A short text per language (label, alt text, statement); <see cref="Secondary"/> is an optional second line.</summary>
public sealed record LocalizedTextDto(string LanguageCode, string Text, string? Secondary = null);
