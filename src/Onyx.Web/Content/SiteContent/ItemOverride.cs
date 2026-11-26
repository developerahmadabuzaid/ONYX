namespace Onyx.Web.Content.SiteContent;

public sealed record ItemOverride
{
    public string? Title { get; init; }

    public string? Text { get; init; }

    public List<string>? Points { get; init; }

    public List<string>? Tags { get; init; }

    public List<string>? Deliverables { get; init; }

    public List<string>? Involvement { get; init; }
}
