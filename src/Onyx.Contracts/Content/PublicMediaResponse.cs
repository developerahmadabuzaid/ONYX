using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record PublicMediaResponse(Guid Id, string FileName, string ContentType, int? Width, int? Height, string? Alt, string? Caption);
