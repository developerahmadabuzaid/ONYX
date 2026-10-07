namespace Onyx.Application.News;

/// <summary>Stable, safe failure codes of a feed read (stored as the source's last error; never include URLs or bodies).</summary>
public static class NewsFeedErrors
{
    /// <summary>The destination is not allowed (private/internal address, scheme, credentials, port, redirect target).</summary>
    public const string Blocked = "Feed.Blocked";

    public const string Timeout = "Feed.Timeout";

    public const string TooLarge = "Feed.TooLarge";

    public const string TooManyRedirects = "Feed.TooManyRedirects";

    /// <summary>The server answered with a non-success status.</summary>
    public const string HttpStatus = "Feed.HttpStatus";

    /// <summary>The site's robots.txt does not allow the feed to be fetched.</summary>
    public const string RobotsDisallowed = "Feed.RobotsDisallowed";

    /// <summary>Not a well-formed feed of the expected format.</summary>
    public const string Malformed = "Feed.Malformed";

    /// <summary>The server could not be reached (DNS, connection, TLS).</summary>
    public const string Unreachable = "Feed.Unreachable";
}
