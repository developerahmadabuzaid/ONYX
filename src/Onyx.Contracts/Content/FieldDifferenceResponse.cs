using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record FieldDifferenceResponse(string? LanguageCode, string Field, string? Before, string? After);
