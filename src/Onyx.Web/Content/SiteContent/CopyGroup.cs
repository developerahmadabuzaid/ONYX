namespace Onyx.Web.Content.SiteContent;

/// <summary>A titled group of editable resource keys (the title is the admin resource <c>SiteContent.Group.{Id}</c>).</summary>
public sealed record CopyGroup(string Id, IReadOnlyList<string> Keys);
