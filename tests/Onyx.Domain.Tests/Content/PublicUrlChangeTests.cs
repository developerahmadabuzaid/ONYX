using Onyx.Domain.Content;

namespace Onyx.Domain.Tests.Content;

public sealed class PublicUrlChangeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Announced_urls_are_deduplicated_and_split_into_bounded_events()
    {
        var item = ContentItemLifecycleTests.CompletePage();
        item.ClearDomainEvents();

        item.AnnouncePublicUrls(Enumerable.Range(0, PublicUrlsChanged.MaxPaths + 1).Select(index => $"/en/page-{index}").Append("/en/page-0"));

        var events = item.DomainEvents.OfType<PublicUrlsChanged>().ToList();
        Assert.Equal(2, events.Count);
        Assert.Equal(PublicUrlsChanged.MaxPaths, events[0].Paths.Count);
        Assert.Single(events[1].Paths);
    }

    [Fact]
    public void Nothing_is_announced_when_no_url_changed()
    {
        var item = ContentItemLifecycleTests.CompletePage();
        item.ClearDomainEvents();

        item.AnnouncePublicUrls([]);

        Assert.Empty(item.DomainEvents.OfType<PublicUrlsChanged>());
    }

    [Fact]
    public void Live_modification_time_is_recorded_per_language()
    {
        var item = ContentItemLifecycleTests.CompletePage();

        item.RecordLiveContentModified("en", Now);

        Assert.Equal(Now, item.GetTranslation("en")!.LiveContentModifiedAtUtc);
        Assert.Null(item.GetTranslation("ar")!.LiveContentModifiedAtUtc);
    }

    [Fact]
    public void Redirect_rules_announce_their_source_url_on_create_update_and_removal()
    {
        var rule = Redirect.Create("ar", "قديم", RedirectKind.Gone, null, null, null).Value;
        Assert.Equal(["/ar/قديم"], Assert.Single(rule.DomainEvents.OfType<PublicUrlsChanged>()).Paths);

        rule.ClearDomainEvents();
        rule.Update(RedirectKind.Permanent, Guid.NewGuid(), null, null, isActive: true);
        rule.AnnounceRemoval();

        Assert.Equal(2, rule.DomainEvents.OfType<PublicUrlsChanged>().Count());
    }
}
