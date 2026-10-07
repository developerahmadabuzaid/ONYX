using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Onyx.API.IntegrationTests.Content;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.API.IntegrationTests.Worker;
using Onyx.Application.Jobs;
using Onyx.Application.News;
using Onyx.Contracts.News;
using Onyx.Domain.Common;
using Onyx.Domain.News;
using Onyx.Infrastructure.BackgroundJobs;

namespace Onyx.API.IntegrationTests.News;

/// <summary>
/// News Hub test plumbing: categories and sources through the admin API, and the real Worker pipeline with two controlled
/// boundaries — the feed reader (network) and the external summary service (Onyx's AI Agent, absent by default).
/// All feed content is obviously fictional sample data.
/// </summary>
public abstract class NewsApiTestBase(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    protected const string Sources = "/api/v1/admin/news-sources";
    protected const string Items = "/api/v1/admin/news";
    protected const string Categories = "/api/v1/admin/news-categories";

    /// <summary>Marker inside every sample excerpt: it must never be found in the database.</summary>
    protected const string ExcerptMarker = "SAMPLE-EXCERPT-NEVER-STORED";

    protected FakeFeedReader Feeds { get; } = new();

    /// <summary>Null: the production default (no summary service connected).</summary>
    protected FakeSummaryService? Summaries { get; set; }

    protected static async Task<NewsCategoryResponse> CreateCategoryAsync(HttpClient client, string key, string english, string arabic, params string[] keywords) =>
        await ReadAsync<NewsCategoryResponse>(await client.PostAsJsonAsync(Categories, new SaveNewsCategoryRequest(
            key, 1, keywords, [new NewsCategoryTranslationDto("ar", arabic, null), new NewsCategoryTranslationDto("en", english, null)], null), Token));

    protected static SaveNewsSourceRequest SourceRequest(string name, string feedUrl, Guid categoryId, bool autoPublish = false, string? note = "Sample terms reviewed for tests.") =>
        new(name, "https://publisher-sample.com", feedUrl, "Rss", "en", categoryId, 60, note, autoPublish, null);

    protected static async Task<NewsSourceResponse> CreateAllowedSourceAsync(HttpClient client, SaveNewsSourceRequest request)
    {
        var created = await ReadAsync<NewsSourceResponse>(await client.PostAsJsonAsync(Sources, request, Token));
        return await ReadAsync<NewsSourceResponse>(await client.PostAsJsonAsync($"{Sources}/{created.Id}/allow", new NewsVersionRequest(created.Version), Token));
    }

    protected static FeedEntry Entry(string slug, string title, DateTimeOffset? published = null, params string[] categories) =>
        new(new Uri($"https://publisher-sample.com/news/{slug}"), title, published, categories, $"{ExcerptMarker} fictional description of {title}.");

    /// <summary>Runs the news-ingestion job once (like the scheduler would), then delivers the outbox (search, notifications).</summary>
    protected async Task<JobOutcome> IngestAsync() => await IngestAsync(null);

    protected async Task<JobOutcome> IngestAsync(IReadOnlyDictionary<string, string?>? settings)
    {
        await using var worker = WorkerTestHost.Create(Fixture.ApplicationDatabase!.ConnectionString, "worker-a", Time, services =>
        {
            services.RemoveAll<INewsFeedReader>();
            services.AddSingleton<INewsFeedReader>(Feeds);
            if (Summaries is not null)
            {
                services.RemoveAll<INewsSummaryService>();
                services.AddSingleton<INewsSummaryService>(Summaries);
            }
        }, settings);

        var outcome = await worker.InScopeAsync(services =>
            services.GetServices<IScheduledJob>().Single(job => job.Name == NewsIngestionJob.JobName).ExecuteAsync(Token));
        await DeliverOutboxAsync(worker);
        return outcome;
    }

    /// <summary>Delivers pending outbox events (search indexing after editor actions in the API).</summary>
    protected async Task DeliverOutboxAsync()
    {
        await using var worker = WorkerTestHost.Create(Fixture.ApplicationDatabase!.ConnectionString, "worker-a", Time);
        await DeliverOutboxAsync(worker);
    }

    protected static async Task<NewsItemResponse> ItemAsync(HttpClient client, Guid id) =>
        await ReadAsync<NewsItemResponse>(await client.GetAsync($"{Items}/{id}", Token));

    protected static async Task<NewsItemResponse> WriteSummariesAsync(HttpClient client, NewsItemResponse item, string english = "Sample headline for tests")
    {
        var withArabic = await ReadAsync<NewsItemResponse>(await client.PutAsJsonAsync($"{Items}/{item.Id}/translations/ar", new UpdateNewsTranslationRequest(
            "عنوان تجريبي للاختبار", "ملخص تجريبي من Onyx مكتوب لأغراض الاختبار فقط، ويتجاوز الحد الأدنى للطول المطلوب.", item.Version), Token));
        return await ReadAsync<NewsItemResponse>(await client.PutAsJsonAsync($"{Items}/{item.Id}/translations/en", new UpdateNewsTranslationRequest(
            english, "A sample Onyx summary written for tests only, longer than the minimum length required.", withArabic.Version), Token));
    }

    private static async Task DeliverOutboxAsync(WorkerTestHost worker)
    {
        var processor = worker.Get<OutboxProcessor>();
        while (await processor.ProcessBatchAsync(Token) > 0)
        {
        }
    }

    /// <summary>Stands in for the network: feed URL → entries or a safe failure code. Records every read.</summary>
    protected sealed class FakeFeedReader : INewsFeedReader
    {
        public ConcurrentDictionary<string, Func<Result<IReadOnlyList<FeedEntry>>>> Feeds { get; } = new(StringComparer.Ordinal);

        public ConcurrentQueue<string> Reads { get; } = new();

        public void Serve(string feedUrl, params FeedEntry[] entries) => Feeds[feedUrl] = () => entries;

        public void Fail(string feedUrl, string code) => Feeds[feedUrl] = () => Error.Failure(code, "Sample failure.");

        public Task<Result<IReadOnlyList<FeedEntry>>> ReadAsync(Uri feedUrl, NewsFeedFormat expectedFormat, int maxEntries, CancellationToken cancellationToken)
        {
            Reads.Enqueue(feedUrl.AbsoluteUri);
            return Task.FromResult(Feeds.TryGetValue(feedUrl.AbsoluteUri, out var respond)
                ? respond()
                : Result.Failure<IReadOnlyList<FeedEntry>>(Error.Failure(NewsFeedErrors.Unreachable, "Sample failure.")));
        }
    }

    /// <summary>Stands in for Onyx's external AI Agent behind the provider-neutral boundary.</summary>
    protected sealed class FakeSummaryService(Func<NewsSummaryRequest, CancellationToken, Task<Result<NewsSummaryDraft>>> respond) : INewsSummaryService
    {
        public ConcurrentQueue<NewsSummaryRequest> Requests { get; } = new();

        public string ProviderKey => "ai.news-summary";

        public static FakeSummaryService Writing(string englishTitle = "Agent sample headline") =>
            new((request, _) => Task.FromResult(Result.Success(new NewsSummaryDraft(
            [
                new NewsText("ar", "عنوان تجريبي من الوكيل", "ملخص تجريبي من الوكيل الخارجي لأغراض الاختبار فقط، وهو أطول من الحد الأدنى المطلوب."),
                new NewsText("en", $"{englishTitle} {request.ItemId.ToString()[^4..]}", "A sample summary from the external agent for tests only, longer than the minimum length."),
            ]))));

        public async Task<Result<NewsSummaryDraft>> SummarizeAsync(NewsSummaryRequest request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request);
            return await respond(request, cancellationToken);
        }
    }
}
