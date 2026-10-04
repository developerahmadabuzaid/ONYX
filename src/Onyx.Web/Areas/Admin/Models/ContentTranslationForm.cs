namespace Onyx.Web.Areas.Admin.Models;

/// <summary>One language of a content item, edited as plain text (see <c>BlockTextFormat</c>).</summary>
public sealed class ContentTranslationForm
{
    public string Title { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string? BodyText { get; set; }

    public string? FaqText { get; set; }

    public uint Version { get; set; }
}
