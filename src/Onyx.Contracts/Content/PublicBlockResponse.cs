using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record PublicBlockResponse(
    string Type,
    int? Level,
    IReadOnlyList<PublicSpanResponse>? Spans,
    IReadOnlyList<IReadOnlyList<PublicSpanResponse>>? Items,
    bool? Ordered,
    PublicMediaResponse? Image,
    PublicCtaResponse? Cta);
