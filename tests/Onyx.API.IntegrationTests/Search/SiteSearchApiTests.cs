using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Content;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.API.IntegrationTests.Worker;
using Onyx.Application.Jobs;
using Onyx.Contracts.Content;
using Onyx.Contracts.Search;
using Onyx.Infrastructure.BackgroundJobs;

namespace Onyx.API.IntegrationTests.Search;

/// <summary>
/// Site search against a real PostgreSQL server (architecture.md §18): server capabilities, Arabic normalization, English
/// stemming, exact names, typo tolerance, publication and indexability rules, and hostile query text. All page text is
/// obviously fictional test data.
/// </summary>
public sealed class SiteSearchApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    [Fact]
    public async Task Server_provides_arabic_and_english_configurations_trigrams_and_the_builtin_utf8_locale()
    {
        Fixture.RequireDatabase();
        await using var db = await DbAsync();

        Assert.Equal(["arabic", "english", "simple"], await ScalarsAsync(db, $"SELECT cfgname::text AS \"Value\" FROM pg_ts_config WHERE cfgname IN ('arabic', 'english', 'simple') ORDER BY cfgname"));
        Assert.Equal(["pg_trgm"], await ScalarsAsync(db, $"SELECT extname::text AS \"Value\" FROM pg_extension WHERE extname = 'pg_trgm'"));
        Assert.Equal(["UTF8 b C.UTF-8"], await ScalarsAsync(db,
            $"SELECT pg_encoding_to_char(encoding) || ' ' || datlocprovider::text || ' ' || coalesce(datlocale, '') AS \"Value\" FROM pg_database WHERE datname = current_database()"));
        Assert.True(int.Parse((await ScalarsAsync(db, $"SELECT current_setting('server_version_num') AS \"Value\""))[0], System.Globalization.CultureInfo.InvariantCulture) >= 170000);

        // The Arabic stemmer really stems (a plural and its singular share a lexeme), it is not silently "simple".
        Assert.Equal(["true"], await ScalarsAsync(db, $"SELECT (to_tsvector('arabic'::regconfig, 'المكتبات') @@ to_tsquery('arabic'::regconfig, 'مكتبه'))::text AS \"Value\""));
        Assert.Equal(["false"], await ScalarsAsync(db, $"SELECT (to_tsvector('simple'::regconfig, 'المكتبات') @@ to_tsquery('simple'::regconfig, 'مكتبه'))::text AS \"Value\""));
    }

    [Fact]
    public async Task Arabic_queries_ignore_diacritics_letter_variants_and_digit_forms()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await PublishAsync(owner, await CreateAsync(owner, "ServicePillar",
            ("Sample automation", "sample-automation", "Sample text."),
            ("أتمتة العمليات الإدارية", "أتمتة-تجريبية", "نص تجريبي عن المستشفى والمؤسسة والهيئة ورمز المرجع ٤٥٦٧ والذكاء الاصطناعي.")));
        await IndexAsync();

        var missed = new List<string>();
        foreach (var query in new[] { "الذَّكَاءُ", "اتمتة", "آتمتة", "الاداريه", "المستشفي", "موسسة", "المؤسسة", "والمؤسسه", "اصطناعي", "الهيئه", "4567", "٤٥٦٧", "۴۵۶۷" })
        {
            if (!(await TitlesAsync("ar", query)).Contains("أتمتة العمليات الإدارية"))
            {
                missed.Add(query);
            }
        }

        Assert.Empty(missed);

        Assert.Empty(await TitlesAsync("ar", "مكتبات"));
    }

    [Fact]
    public async Task English_queries_are_stemmed_names_match_exactly_and_title_typos_are_tolerated()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await PublishAsync(owner, await CreateAsync(owner, "ServicePillar",
            ("Process Automation", "process-automation", "Sample text about automating document workflows."),
            ("أتمتة تجريبية", "أتمتة-تجريبية", "نص تجريبي.")));
        await PublishAsync(owner, await CreateAsync(owner, "Page",
            ("Zentrixa Sample Brand", "zentrixa", "A fictional brand name used only in tests."),
            ("علامة تجريبية", "علامة-تجريبية", "نص تجريبي.")));
        await IndexAsync();

        Assert.Contains("Process Automation", await TitlesAsync("en", "automating"));   // automating, automation → autom
        Assert.Contains("Process Automation", await TitlesAsync("en", "workflow"));
        Assert.Contains("Process Automation", await TitlesAsync("en", "Automaton"));    // typo: trigram title similarity
        Assert.Contains("Process Automation", await TitlesAsync("en", "proc"));         // prefix of the last word
        Assert.Equal(["Zentrixa Sample Brand"], await TitlesAsync("en", "ZENTRIXA"));
        Assert.Contains("Zentrixa Sample Brand", await TitlesAsync("en", "Zentrxia"));  // transposed letters

        var grouped = await SearchAsync("en", "sample");
        Assert.Equal(["Services", "Pages"], grouped.Groups.Select(group => group.Group));
        Assert.Equal("/en/services/process-automation", grouped.Groups[0].Items.Single().Path);
        Assert.Equal(grouped.Total, grouped.Groups.Sum(group => group.Total));
    }

    [Fact]
    public async Task Each_language_searches_only_its_own_documents()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await PublishAsync(owner, await CreateAsync(owner, "Page",
            ("Quarzelle Overview", "quarzelle", "Sample text."),
            ("نظرة كوارزيل", "كوارزيل", "نص تجريبي.")));
        await IndexAsync();

        Assert.Equal(["Quarzelle Overview"], await TitlesAsync("en", "quarzelle"));
        Assert.Empty(await TitlesAsync("ar", "quarzelle"));
        Assert.Equal(["نظرة كوارزيل"], await TitlesAsync("ar", "كوارزيل"));
        Assert.Empty(await TitlesAsync("en", "كوارزيل"));
    }

    [Fact]
    public async Task Drafts_unverified_proof_noindex_and_offline_pages_are_never_returned()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await CreateAsync(owner, "Page", ("Velmora Draft", "velmora-draft", "Sample."), ("مسودة فيلمورا", "مسودة-فيلمورا", "نص."));

        var project = await CreateAsync(owner, "Project", ("Velmora Project", "velmora-project", "Sample."), ("مشروع فيلمورا", "مشروع-فيلمورا", "نص."));
        await owner.PostAsJsonAsync($"{Admin}/{project.Id}/submit", new ContentVersionRequest(project.Version), Token);

        var hidden = await CreateAsync(owner, "Page", ("Velmora Hidden", "velmora-hidden", "Sample."), ("مخفية فيلمورا", "مخفية-فيلمورا", "نص."));
        var noindex = new SeoProfileDto(null, null, null, null, null, null, false, true, ["WebPage"], null, null, null, null, null, null);
        hidden = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{hidden.Id}/translations/en/seo", new UpdateContentSeoRequest(noindex, hidden.Version), Token));
        await PublishAsync(owner, hidden);

        var live = await PublishAsync(owner, await CreateAsync(owner, "Page", ("Velmora Live", "velmora-live", "Sample."), ("حية فيلمورا", "حية-فيلمورا", "نص.")));
        await IndexAsync();
        await ReconcileAsync();

        Assert.Equal(["Velmora Live"], await TitlesAsync("en", "velmora"));
        Assert.Equal(["حية فيلمورا", "مخفية فيلمورا"], (await TitlesAsync("ar", "فيلمورا")).Order(StringComparer.Ordinal));

        // Taking the page offline removes it from the index through the outbox event.
        await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{live.Id}/unpublish", new TakeOfflineRequest("PendingDecision", null, live.Version), Token));
        await IndexAsync();
        Assert.Empty(await TitlesAsync("en", "velmora"));
        Assert.Empty(await QueryAsync(db => db.SearchDocuments.Where(document => document.ContentItemId == live.Id)));
    }

    [Fact]
    public async Task Reconcile_fills_the_index_once_and_slug_changes_reindex_the_new_path()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await PublishAsync(owner, await CreateAsync(owner, "Page", ("Ostrava Sample", "ostrava", "Sample."), ("أوسترافا تجريبية", "أوسترافا", "نص.")));

        // Without the outbox, the scheduled reconcile builds the index; a second run has nothing to do.
        Assert.False((await ReconcileAsync()).WasSkipped);
        Assert.True((await ReconcileAsync()).WasSkipped);
        Assert.Equal(2, (await QueryAsync(db => db.SearchDocuments.Where(document => document.ContentItemId == page.Id))).Count);

        var renamed = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en", Translation("Ostrava Sample", "ostrava-renamed", page.Version), Token));
        await PublishAsync(owner, renamed);
        await IndexAsync();

        var result = await SearchAsync("en", "ostrava");
        Assert.Equal("/en/ostrava-renamed", result.Groups.Single().Items.Single().Path);
        Assert.True((await ReconcileAsync()).WasSkipped);
    }

    [Fact]
    public async Task Hostile_query_text_is_only_ever_text()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await PublishAsync(owner, await CreateAsync(owner, "Page", ("Brindle Sample", "brindle", "Sample."), ("برندل تجريبية", "برندل", "نص.")));
        await IndexAsync();

        foreach (var hostile in new[]
        {
            "'; DROP TABLE search_documents; --", "brindle' OR '1'='1", "brindle:* | !x & (y", "\" -\" -- ''", "\\x00 %_ $$ ;",
            "\"brindle sample\" -nothing", "brindle OR or -",
        })
        {
            using var response = await Client.GetAsync($"/api/v1/search?language=en&q={Uri.EscapeDataString(hostile)}", Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(["Brindle Sample"], await TitlesAsync("en", "\"brindle sample\""));
        Assert.Empty(await TitlesAsync("en", "brindle -sample"));    // excluded on every matching path (stems, names, trigrams)
        Assert.Empty(await TitlesAsync("en", "brindle -other -samples"));
        Assert.Equal(["Brindle Sample"], await TitlesAsync("en", "brindle -other"));
        Assert.Empty(await TitlesAsync("en", "-brindle"));
        Assert.Single(await QueryAsync(db => db.SearchDocuments.Where(document => document.LanguageCode == "en")));
    }

    [Fact]
    public async Task Requests_are_validated_capped_and_never_cached()
    {
        Fixture.RequireDatabase();

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.GetAsync("/api/v1/search?language=fr&q=sample", Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.GetAsync("/api/v1/search?q=sample", Token)));

        using var response = await Client.GetAsync($"/api/v1/search?language=en&q={new string('x', 150)}", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await ReadAsync<SiteSearchResponse>(response);
        Assert.Equal(100, body.Query.Length);
        Assert.Empty(body.Groups);

        var tooShort = await SearchAsync("ar", " ً ");
        Assert.Equal(0, tooShort.Total);
    }

    private static async Task<ContentDetailResponse> CreateAsync(
        HttpClient client, string type, (string Title, string Slug, string Body) english, (string Title, string Slug, string Body) arabic)
    {
        var created = await ReadAsync<ContentDetailResponse>(await client.PostAsJsonAsync(Admin, new CreateContentRequest(type, null, null, null, 0), Token));
        var withArabic = await ReadAsync<ContentDetailResponse>(await client.PutAsJsonAsync(
            $"{Admin}/{created.Id}/translations/ar", Text(arabic, created.Version), Token));
        return await ReadAsync<ContentDetailResponse>(await client.PutAsJsonAsync(
            $"{Admin}/{created.Id}/translations/en", Text(english, withArabic.Version), Token));
    }

    private static UpdateContentTranslationRequest Text((string Title, string Slug, string Body) text, uint version) =>
        new(text.Title, $"{text.Title}.", text.Slug, [new ContentBlockDto("Paragraph", Spans: [new TextSpanDto(text.Body)])], [], version);

    private async Task<SiteSearchResponse> SearchAsync(string language, string query) =>
        await ReadAsync<SiteSearchResponse>(await Client.GetAsync($"/api/v1/search?language={language}&q={Uri.EscapeDataString(query)}", Token));

    private async Task<List<string>> TitlesAsync(string language, string query) =>
        (await SearchAsync(language, query)).Groups.SelectMany(group => group.Items).Select(item => item.Title).ToList();

    /// <summary>Delivers pending outbox events (publication and URL changes) to the Worker's search handlers.</summary>
    private async Task IndexAsync()
    {
        await using var worker = WorkerTestHost.Create(Fixture.ApplicationDatabase!.ConnectionString, "worker-a", Time);
        var processor = worker.Get<OutboxProcessor>();
        while (await processor.ProcessBatchAsync(Token) > 0)
        {
        }
    }

    private async Task<JobOutcome> ReconcileAsync()
    {
        await using var worker = WorkerTestHost.Create(Fixture.ApplicationDatabase!.ConnectionString, "worker-a", Time);
        return await worker.InScopeAsync(services =>
            services.GetServices<IScheduledJob>().Single(job => job.Name == SearchIndexReconcileJob.JobName).ExecuteAsync(Token));
    }

    private static Task<List<string>> ScalarsAsync(Microsoft.EntityFrameworkCore.DbContext db, FormattableString sql) =>
        db.Database.SqlQuery<string>(sql).ToListAsync(Token);
}
