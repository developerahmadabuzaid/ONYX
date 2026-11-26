namespace Onyx.Web.Content.SiteContent;

/// <summary>The overrides used for this request: the published snapshot, or an admin preview that swaps in one draft.</summary>
public sealed class SiteContentContext(SiteContentProvider provider)
{
    private SiteContentSnapshot? _preview;

    public SiteContentSnapshot Snapshot => _preview ?? provider.Current;

    public bool IsPreview => _preview is not null;

    /// <summary>Admin preview only (the controller is authenticated and the page is noindex).</summary>
    public void UsePreview(string key, string? draftJson) => _preview = provider.Current.With(key, draftJson);
}
