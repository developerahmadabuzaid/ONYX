using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>A structured body block. <see cref="Type"/>: Heading, Paragraph, List, Image or CallToAction. Unused fields are null.</summary>
public sealed record ContentBlockDto(
    string Type,
    int? Level = null,
    IReadOnlyList<TextSpanDto>? Spans = null,
    IReadOnlyList<ListItemDto>? Items = null,
    bool? Ordered = null,
    Guid? MediaAssetId = null,
    string? Alt = null,
    string? Caption = null,
    Guid? CtaId = null);
