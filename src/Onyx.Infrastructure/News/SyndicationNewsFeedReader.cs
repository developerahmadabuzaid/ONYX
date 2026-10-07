using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Logging;
using Onyx.Application.News;
using Onyx.Domain.Common;
using Onyx.Domain.News;
using Onyx.Infrastructure.Outbound;

namespace Onyx.Infrastructure.News;

/// <summary>
/// Reads RSS 2.0 and Atom 1.0 feeds (System.ServiceModel.Syndication, owner-approved; used only here). The feed is fetched by
/// <see cref="SafeOutboundHttpClient"/> (https only, size- and time-capped, SSRF-checked) after the site's robots.txt allows it,
/// then parsed from memory with DTDs prohibited, no XML resolver and a character limit, so entity expansion and external
/// references are impossible. A bad date or link affects only that entry. Only titles, links, dates, category labels and a
/// short plain-text description are returned; nothing is stored or logged here.
/// </summary>
public sealed partial class SyndicationNewsFeedReader(SafeOutboundHttpClient http, RobotsTxtPolicy robots, ILogger<SyndicationNewsFeedReader> logger) : INewsFeedReader
{
    public const int MaxExcerptCharacters = 2_000;
    public const int MaxTitleCharacters = 1_000;
    public const int MaxCategories = 10;
    public const int MaxCategoryCharacters = 100;

    private const string Accept = "application/rss+xml, application/atom+xml, application/xml;q=0.9, text/xml;q=0.8";

    /// <summary>Marks an unusable link inside the parser; rejected later as not http(s).</summary>
    private static readonly Uri InvalidLink = new("about:invalid");

    public async Task<Result<IReadOnlyList<FeedEntry>>> ReadAsync(Uri feedUrl, NewsFeedFormat expectedFormat, int maxEntries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(feedUrl);
        SafeOutboundResponse response;
        try
        {
            if (!await robots.IsAllowedAsync(feedUrl, requireHttps: true, cancellationToken).ConfigureAwait(false))
            {
                return Error.Failure(NewsFeedErrors.RobotsDisallowed, "The site's robots.txt does not allow this feed to be fetched.");
            }

            response = await http.GetAsync(feedUrl, new SafeOutboundRequest(RequireHttps: true, Accept), cancellationToken).ConfigureAwait(false);
        }
        catch (SafeOutboundException exception)
        {
            LogFetchRefused(logger, exception.Code);
            return Error.Failure(exception.Code, exception.Message);
        }

        return Parse(response.Body, expectedFormat, maxEntries);
    }

    /// <summary>Parses an in-memory feed document with all XML protections applied.</summary>
    public static Result<IReadOnlyList<FeedEntry>> Parse(byte[] document, NewsFeedFormat expectedFormat, int maxEntries)
    {
        ArgumentNullException.ThrowIfNull(document);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = Math.Max(document.Length, 1),
            MaxCharactersFromEntities = 1_024,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CloseInput = true,
        };

        SyndicationFeedFormatter formatter = expectedFormat == NewsFeedFormat.Atom ? new Atom10FeedFormatter() : new Rss20FeedFormatter();
        formatter.DateTimeParser = (XmlDateTimeData data, out DateTimeOffset value) =>
        {
            // Unusable dates become "unknown" rather than failing the whole feed.
            value = FeedText.ParseDate(data.DateTimeString) ?? DateTimeOffset.MinValue;
            return true;
        };
        formatter.UriParser = (XmlUriData data, out Uri uri) =>
        {
            uri = Uri.TryCreate(data.UriString?.Trim(), data.UriKind, out var parsed) ? parsed : InvalidLink;
            return true;
        };

        try
        {
            // Some servers emit whitespace before the XML declaration; skip it (after an optional UTF-8 BOM) instead of failing.
            var start = document.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? 3 : 0;
            while (start < document.Length && document[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                start++;
            }

            using var stream = new MemoryStream(document, start, document.Length - start, writable: false);
            using var reader = XmlReader.Create(stream, settings);
            if (!formatter.CanRead(reader))
            {
                return Error.Failure(NewsFeedErrors.Malformed, $"The document is not an {(expectedFormat == NewsFeedFormat.Atom ? "Atom 1.0" : "RSS 2.0")} feed.");
            }

            formatter.ReadFrom(reader);
            return formatter.Feed.Items.Take(maxEntries).Select(ToEntry).Where(entry => entry.Title.Length > 0).ToList();
        }
        catch (Exception exception) when (exception is XmlException or FormatException or InvalidOperationException or ArgumentException)
        {
            return Error.Failure(NewsFeedErrors.Malformed, "The feed could not be read.");
        }
    }

    private static FeedEntry ToEntry(SyndicationItem item)
    {
        var link = item.Links
            .Where(candidate => candidate.Uri is { IsAbsoluteUri: true } && (string.IsNullOrEmpty(candidate.RelationshipType) || candidate.RelationshipType == "alternate"))
            .Select(candidate => candidate.Uri)
            .FirstOrDefault();
        if (link is null && Uri.TryCreate(item.Id, UriKind.Absolute, out var permalink))
        {
            link = permalink;
        }

        var title = FeedText.ToPlainText(item.Title?.Text, MaxTitleCharacters) ?? string.Empty;
        var published = Known(item.PublishDate) ?? Known(item.LastUpdatedTime);
        var categories = item.Categories
            .Select(category => FeedText.ToPlainText(category.Label ?? category.Name, MaxCategoryCharacters))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(MaxCategories)
            .ToList();
        var excerpt = FeedText.ToPlainText((item.Summary as TextSyndicationContent)?.Text ?? (item.Content as TextSyndicationContent)?.Text, MaxExcerptCharacters);
        return new FeedEntry(link == InvalidLink ? null : link, title, published, categories, excerpt);
    }

    private static DateTimeOffset? Known(DateTimeOffset value) => value == DateTimeOffset.MinValue || value == default ? null : value.ToUniversalTime();

    [LoggerMessage(Level = LogLevel.Warning, Message = "News feed fetch refused or failed with {Code}")]
    private static partial void LogFetchRefused(ILogger logger, string code);
}
