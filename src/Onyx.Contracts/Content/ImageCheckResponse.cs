using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record ImageCheckResponse(Guid MediaAssetId, bool Exists, bool HasAlt, string? Alt, int? Width, int? Height, string Context);
