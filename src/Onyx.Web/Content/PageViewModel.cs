using Onyx.Contracts.Content;

namespace Onyx.Web.Content;

/// <summary>A content page as rendered by the public site; <see cref="PreviewToken"/> is set only for draft previews.</summary>
public sealed record PageViewModel(PublicContentResponse Content, string? PreviewToken = null)
{
    /// <summary>Same-origin media URL; previews pass their token so the API can authorize working-copy images.</summary>
    public string MediaUrl(Guid id) =>
        PreviewToken is null ? $"/media/{id:D}" : $"/media/{id:D}?preview={Uri.EscapeDataString(PreviewToken)}";
}
