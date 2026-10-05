using Onyx.Application.Content;
using Onyx.Domain.Content;

namespace Onyx.Application.Tests.Content;

public sealed class WorkAndInsightRulesTests
{
    [Theory]
    [InlineData(ContentType.Insight, true)]
    [InlineData(ContentType.Page, false)]
    [InlineData(ContentType.Project, false)]
    [InlineData(ContentType.GlossaryTerm, false)]
    public void Article_structured_data_is_served_only_for_insights(ContentType type, bool served) =>
        Assert.Equal(served, PublicContentBuilder.SupportedStructuredData(type, StructuredDataTypes.WebPage | StructuredDataTypes.Article, 0, 1).Contains("Article"));

    [Fact]
    public void The_insight_byline_and_provenance_are_part_of_the_public_page()
    {
        var insight = Snapshot();

        Assert.NotEqual(PublicPageFingerprint.For(insight, "en"), PublicPageFingerprint.For(insight with { AuthorId = Guid.NewGuid() }, "en"));
        Assert.NotEqual(PublicPageFingerprint.For(insight, "en"), PublicPageFingerprint.For(insight with { Origin = InsightOrigin.AiAssisted }, "en"));
        Assert.NotEqual(PublicPageFingerprint.For(insight, "en"), PublicPageFingerprint.For(insight with { InsightKind = InsightKind.Guide }, "en"));
    }

    [Fact]
    public void Older_snapshots_without_insight_fields_still_deserialize()
    {
        const string stage6Snapshot =
            """{"type":"Page","parentId":null,"key":null,"presentation":"Page","sortOrder":0,"primaryCtaId":null,"featuredImageId":null,"translations":[]}""";

        var snapshot = ContentSnapshotSerializer.Deserialize(stage6Snapshot);

        Assert.Equal(InsightOrigin.Human, snapshot.Origin);
        Assert.Null(snapshot.InsightKind);
        Assert.Null(snapshot.AuthorId);
    }

    private static ContentSnapshot Snapshot() =>
        new(ContentType.Insight, null, null, ContentPresentation.Page, 0, null, null,
            [new TranslationSnapshot("en", "Insight", "Summary", "insight", [], [], new SeoProfile())], InsightKind.Article, InsightOrigin.Human, null);
}
