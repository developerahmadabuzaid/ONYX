namespace Onyx.Domain.News;

/// <summary>Stored as text. The format the source is expected to publish; a feed in another format is rejected as malformed.</summary>
public enum NewsFeedFormat
{
    Rss = 0,
    Atom = 1,
}
