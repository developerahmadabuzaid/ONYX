namespace Onyx.Domain.Content;

/// <summary>
/// One structured body block. Bodies are never stored as HTML: every string is plain text, encoded when rendered,
/// so there is no stored-markup path to XSS. Fields not used by <see cref="Type"/> must be null.
/// </summary>
public sealed record ContentBlock(
    ContentBlockType Type,
    int? Level = null,
    IReadOnlyList<TextSpan>? Spans = null,
    IReadOnlyList<ListItem>? Items = null,
    bool? Ordered = null,
    Guid? MediaAssetId = null,
    string? Alt = null,
    string? Caption = null,
    Guid? CtaId = null);
