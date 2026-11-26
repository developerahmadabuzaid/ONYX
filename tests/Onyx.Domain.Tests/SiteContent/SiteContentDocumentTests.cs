using Onyx.Domain.SiteContent;

namespace Onyx.Domain.Tests.SiteContent;

public sealed class SiteContentDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("home")]
    [InlineData("shared")]
    [InlineData("settings")]
    [InlineData("page:services.cloud")]
    [InlineData("page:legal.privacy-policy")]
    public void Known_keys_are_accepted(string key) => Assert.True(SiteContentDocument.Create(key).IsSuccess);

    [Theory]
    [InlineData("")]
    [InlineData("Home")]
    [InlineData("page:")]
    [InlineData("page:../etc")]
    [InlineData("page:Services")]
    [InlineData("footer")]
    [InlineData("page:a b")]
    public void Unknown_or_malformed_keys_are_rejected(string key) => Assert.True(SiteContentDocument.Create(key).IsFailure);

    [Fact]
    public void Publishing_moves_the_draft_live_numbers_the_revision_and_clears_the_draft()
    {
        var document = SiteContentDocument.Create("home").Value;
        var userId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();

        Assert.True(document.Publish(Now, userId).IsFailure);
        Assert.True(document.SaveDraft("{\"a\":1}", [mediaId, mediaId], Now, userId).IsSuccess);
        Assert.Equal([mediaId], document.DraftMediaIds);

        var revision = document.Publish(Now.AddMinutes(1), userId);

        Assert.True(revision.IsSuccess);
        Assert.Equal(1, revision.Value.Number);
        Assert.Equal("{\"a\":1}", document.PublishedJson);
        Assert.Equal([mediaId], document.PublishedMediaIds);
        Assert.False(document.HasDraft);
        Assert.Empty(document.DraftMediaIds);
        Assert.Equal(1, document.PublishedRevision);
        Assert.Equal(Now.AddMinutes(1), document.PublishedAtUtc);
    }

    [Fact]
    public void Discarding_keeps_the_live_version()
    {
        var document = SiteContentDocument.Create("shared").Value;
        document.SaveDraft("{\"live\":true}", [], Now, null);
        document.Publish(Now, null);
        document.SaveDraft("{\"live\":false}", [], Now, null);

        Assert.True(document.DiscardDraft().IsSuccess);

        Assert.Equal("{\"live\":true}", document.PublishedJson);
        Assert.False(document.HasDraft);
        Assert.True(document.DiscardDraft().IsFailure);
    }

    [Fact]
    public void Restoring_copies_a_revision_into_the_draft_without_publishing_it()
    {
        var document = SiteContentDocument.Create("home").Value;
        document.SaveDraft("{\"v\":1}", [], Now, null);
        var first = document.Publish(Now, null).Value;
        document.SaveDraft("{\"v\":2}", [], Now, null);
        document.Publish(Now, null);

        Assert.True(document.RestoreToDraft(first, Now, null).IsSuccess);

        Assert.Equal("{\"v\":1}", document.DraftJson);
        Assert.Equal("{\"v\":2}", document.PublishedJson);
    }

    [Fact]
    public void A_revision_of_another_document_cannot_be_restored()
    {
        var other = SiteContentDocument.Create("shared").Value;
        other.SaveDraft("{}", [], Now, null);
        var revision = other.Publish(Now, null).Value;
        var document = SiteContentDocument.Create("home").Value;

        Assert.True(document.RestoreToDraft(revision, Now, null).IsFailure);
    }

    [Fact]
    public void Oversized_documents_and_too_many_media_references_are_rejected()
    {
        var document = SiteContentDocument.Create("home").Value;

        Assert.True(document.SaveDraft(" ", [], Now, null).IsFailure);
        Assert.True(document.SaveDraft(new string('a', SiteContentDocument.JsonMaxLength + 1), [], Now, null).IsFailure);
        Assert.True(document.SaveDraft("{}", Enumerable.Range(0, SiteContentDocument.MaxMediaReferences + 1).Select(_ => Guid.NewGuid()).ToList(), Now, null).IsFailure);
        Assert.False(document.HasDraft);
    }
}
