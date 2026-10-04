using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;

namespace Onyx.Domain.Tests.Content;

public sealed class GovernanceRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Metrics_are_accepted_only_from_connected_providers_with_a_period()
    {
        var metrics = new Dictionary<string, double> { ["clicks"] = 12 };

        Assert.NotNull(new EvidenceItem(EvidenceSource.Editor, "Looks popular", Metrics: metrics).Problem());
        Assert.NotNull(new EvidenceItem(EvidenceSource.RuleEngine, "Computed", Metrics: metrics).Problem());
        Assert.NotNull(new EvidenceItem(EvidenceSource.SearchConsole, "Clicks", Metrics: metrics).Problem());
        Assert.Null(new EvidenceItem(EvidenceSource.SearchConsole, "Clicks", PeriodStartUtc: Now.AddDays(-28), PeriodEndUtc: Now, Metrics: metrics).Problem());
        Assert.Null(new EvidenceItem(EvidenceSource.RuleEngine, "Page has no internal links").Problem());
    }

    [Fact]
    public void An_evidence_based_recommendation_must_carry_evidence()
    {
        var withoutEvidence = SeoRecommendation.Propose(
            RecommendationKind.MetaTitle, RecommendationOrigin.Editor, RecommendationBasis.Evidence, "Shorter title", "Title too long", null, [], [], null, "en", null);
        var estimated = SeoRecommendation.Propose(
            RecommendationKind.MetaTitle, RecommendationOrigin.Editor, RecommendationBasis.Estimated, "Shorter title", "Title too long", null, [], [], null, "en", null);

        Assert.True(withoutEvidence.IsFailure);
        Assert.True(estimated.IsSuccess);
        Assert.Equal(RecommendationStatus.Proposed, estimated.Value.Status);
    }

    [Fact]
    public void A_recommendation_cannot_smuggle_fabricated_metrics()
    {
        var fabricated = new EvidenceItem(EvidenceSource.Editor, "Volume", Metrics: new Dictionary<string, double> { ["searchVolume"] = 5_400 });

        var result = SeoRecommendation.Propose(
            RecommendationKind.PrimaryKeyword, RecommendationOrigin.Ai, RecommendationBasis.Evidence, "Target keyword", "No keyword", null, [], [fabricated], null, "en", null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Knowledge_needs_a_recorded_source_before_approval()
    {
        var entry = KnowledgeEntry.Create(KnowledgeCategory.CompanyInformation, "Offices", [new LocalizedText("en", "Offices in Riyadh and Amman.")], null).Value;

        Assert.True(entry.Approve(Guid.NewGuid(), Now).IsFailure);
        Assert.Equal(KnowledgeStatus.Draft, entry.Status);

        Assert.True(entry.Update(KnowledgeCategory.CompanyInformation, "Offices", [new LocalizedText("en", "Offices in Riyadh and Amman.")], "Confirmed by the owner").IsSuccess);
        Assert.True(entry.Approve(Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(KnowledgeStatus.Approved, entry.Status);
    }

    [Fact]
    public void The_brand_kit_starts_empty_and_rejects_invalid_values()
    {
        var kit = BrandKit.CreateEmpty();

        Assert.Null(kit.OfficialLogoMediaId);
        Assert.Null(kit.WebsiteUrl);
        Assert.Empty(kit.Colors);

        Assert.True(kit.Update(null, "http://insecure.example", null, [], [], false, false, null).IsFailure);
        Assert.True(kit.Update(null, null, null, [new BrandColor("Ink", "black", null)], [], false, false, null).IsFailure);
        Assert.True(kit.Update(null, null, "not-an-email", [], [], false, false, null).IsFailure);
    }

    [Fact]
    public void Calendar_entries_are_proposals_that_a_person_decides()
    {
        var entry = ContentCalendarEntry.Propose(
            "Topic", "en", null, null, null, null, SearchIntent.Informational, null, [], null, RecommendationBasis.InsufficientData, [], RecommendationOrigin.Editor).Value;

        Assert.Equal(CalendarEntryStatus.Proposed, entry.Status);
        Assert.True(entry.Complete().IsFailure);
        Assert.True(entry.Accept(Guid.NewGuid(), null, Now).IsSuccess);
        Assert.True(entry.Reject(Guid.NewGuid(), null, Now).IsFailure);
    }

    [Fact]
    public void WhatsApp_calls_to_action_are_not_available_at_launch() =>
        Assert.True(CallToAction.Create("whatsapp", CtaActionType.WhatsApp, null, [new LocalizedText("en", "Chat")]).IsFailure);

    [Fact]
    public void Url_calls_to_action_need_a_safe_https_target()
    {
        Assert.True(CallToAction.Create("docs", CtaActionType.Url, "javascript:alert(1)", [new LocalizedText("en", "Docs")]).IsFailure);
        Assert.True(CallToAction.Create("docs", CtaActionType.Url, "https://example.org/docs", [new LocalizedText("en", "Docs")]).IsSuccess);
    }
}
