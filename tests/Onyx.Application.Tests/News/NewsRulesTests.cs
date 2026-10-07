using Onyx.Application.News;
using Onyx.Domain.News;

namespace Onyx.Application.Tests.News;

public sealed class NewsRulesTests
{
    private static readonly string[] Languages = ["ar", "en"];
    private static readonly Guid DefaultCategory = Guid.CreateVersion7();

    [Fact]
    public void Classification_uses_keywords_and_feed_categories_and_falls_back_to_the_source_default()
    {
        var security = Category("cybersecurity", 4, "Cybersecurity", "الأمن السيبراني", "ransomware", "phishing");
        var ai = Category("artificial-intelligence", 1, "Artificial Intelligence", "الذكاء الاصطناعي", "machine learning", "الذكاء الاصطناعي");
        var categories = new[] { ai, security };

        Assert.Equal(security.Id, NewsClassifier.Classify(categories, DefaultCategory, Entry("New Ransomware campaign targets hospitals")));
        Assert.Equal(ai.Id, NewsClassifier.Classify(categories, DefaultCategory, Entry("Advances in machine learning chips")));
        Assert.Equal(ai.Id, NewsClassifier.Classify(categories, DefaultCategory, Entry("تطورات في الذكاءِ الاصطناعيّ")));            // diacritics ignored
        Assert.Equal(security.Id, NewsClassifier.Classify(categories, DefaultCategory, Entry("Quarterly update", "Cybersecurity")));  // feed category name
        Assert.Equal(DefaultCategory, NewsClassifier.Classify(categories, DefaultCategory, Entry("Company opens a new office")));
    }

    [Fact]
    public void Generated_summaries_must_cover_every_language_once()
    {
        var english = new NewsText("en", "Sample headline", "A sample summary in Onyx's own words that is long enough for the checks.");
        Assert.Equal("NewsSummary.Languages", NewsSummaryChecks.Verify(new NewsSummaryDraft([english]), Languages, "Original", null).Error!.Code);
        Assert.Equal("NewsSummary.Languages", NewsSummaryChecks.Verify(new NewsSummaryDraft([english, english]), Languages, "Original", null).Error!.Code);
        Assert.True(NewsSummaryChecks.Verify(new NewsSummaryDraft([Arabic(), english]), Languages, "Original", null).IsSuccess);
    }

    [Fact]
    public void Summaries_that_copy_the_source_or_carry_links_or_markup_are_rejected()
    {
        const string excerpt = "The vendor said on Tuesday that the new chip doubles inference speed for large language models in data centres worldwide.";
        var copied = new NewsText("en", "Sample headline", "The vendor said on Tuesday that the new chip doubles inference speed for large models.");
        var linked = new NewsText("en", "Sample headline", "Read more at https://publisher.example.com about the announcement and its context.");
        var markup = new NewsText("en", "Sample headline", "<b>Bold</b> claims about the announcement and its wider context for readers.");
        var original = new NewsText("en", "Chipmaker reports faster inference", "A chipmaker reports that its new processor runs AI models faster; details are in the original report.");

        Assert.Equal("NewsSummary.Verbatim", NewsSummaryChecks.Verify(new NewsSummaryDraft([Arabic(), copied]), Languages, "Original", excerpt).Error!.Code);
        Assert.Equal("NewsSummary.Links", NewsSummaryChecks.Verify(new NewsSummaryDraft([Arabic(), linked]), Languages, "Original", excerpt).Error!.Code);
        Assert.Equal("News.MarkupNotAllowed", NewsSummaryChecks.Verify(new NewsSummaryDraft([Arabic(), markup]), Languages, "Original", excerpt).Error!.Code);
        Assert.True(NewsSummaryChecks.Verify(new NewsSummaryDraft([Arabic(), original]), Languages, "Original", excerpt).IsSuccess);
    }

    [Fact]
    public void The_summary_budget_stops_after_the_service_reports_it_is_not_configured()
    {
        var budget = new NewsSummaryBudget(3);
        Assert.True(budget.TryTake());
        budget.RecordNotConfigured();
        Assert.False(budget.TryTake());
        Assert.Equal(1, budget.Requested);
    }

    private static NewsText Arabic() => new("ar", "عنوان تجريبي", "ملخص تجريبي مكتوب بصياغة Onyx الخاصة لأغراض الاختبار فقط، وهو أطول من الحد الأدنى.");

    private static FeedEntry Entry(string title, params string[] categories) => new(new Uri("https://publisher.example.com/story"), title, null, categories, null);

    private static NewsCategory Category(string key, int order, string english, string arabic, params string[] keywords)
    {
        var category = NewsCategory.Create(key, order).Value;
        category.SetTranslation("en", english, null);
        category.SetTranslation("ar", arabic, null);
        category.SetKeywords(NewsClassifier.NormalizeKeywords(keywords));
        return category;
    }
}
