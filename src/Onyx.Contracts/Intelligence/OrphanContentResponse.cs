using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

public sealed record OrphanContentResponse(Guid ContentItemId, string LanguageCode, string Title, string Path);
