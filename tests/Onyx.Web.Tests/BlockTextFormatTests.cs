using Onyx.Contracts.Content;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Tests;

public sealed class BlockTextFormatTests
{
    private static readonly Guid Media = Guid.Parse("0192f7c4-0000-7000-8000-000000000001");
    private static readonly Guid Cta = Guid.Parse("0192f7c4-0000-7000-8000-000000000002");
    private static readonly Guid Target = Guid.Parse("0192f7c4-0000-7000-8000-000000000003");

    [Fact]
    public void Text_is_parsed_into_structured_blocks_without_any_html()
    {
        var text = $"## Heading\n\nA **strong** and *soft* [link](content:{Target}) and [site](/en/services) and [web](https://example.org).\n\n- one\n- two\n\n1. first\n2. second\n\n![Alt text](media:{Media} \"Caption\")\n\n[[cta:{Cta}]]";

        var result = BlockTextFormat.Parse(text);

        Assert.Empty(result.Errors);
        Assert.Equal(["Heading", "Paragraph", "List", "List", "Image", "CallToAction"], result.Blocks.Select(block => block.Type));
        var paragraph = result.Blocks[1].Spans!;
        Assert.Contains(paragraph, span => span.Text == "strong" && span.Emphasis == "Strong");
        Assert.Contains(paragraph, span => span.Text == "soft" && span.Emphasis == "Emphasis");
        Assert.Contains(paragraph, span => span.Link?.ContentItemId == Target);
        Assert.Contains(paragraph, span => span.Link?.Path == "/en/services");
        Assert.Contains(paragraph, span => span.Link?.Url == "https://example.org");
        Assert.True(result.Blocks[3].Ordered);
        Assert.Equal(Media, result.Blocks[4].MediaAssetId);
        Assert.Equal("Caption", result.Blocks[4].Caption);
        Assert.Equal(Cta, result.Blocks[5].CtaId);
    }

    [Fact]
    public void Formatting_and_parsing_round_trip()
    {
        var blocks = new List<ContentBlockDto>
        {
            new("Heading", Level: 3, Spans: [new TextSpanDto("Title * with [brackets]")]),
            new("Paragraph", Spans: [new TextSpanDto("## not a heading "), new TextSpanDto("bold", "Strong", new LinkTargetDto(Path: "/ar/الخدمات"))]),
            new("Paragraph", Spans: [new TextSpanDto("1. not a list")]),
            new("List", Ordered: false, Items: [new ListItemDto([new TextSpanDto("item")])]),
            new("Image", MediaAssetId: Media, Alt: "Alt [x]", Caption: "Cap"),
        };

        var parsed = BlockTextFormat.Parse(BlockTextFormat.Format(blocks));

        Assert.Empty(parsed.Errors);
        Assert.Equal(blocks.Select(block => block.Type), parsed.Blocks.Select(block => block.Type));
        Assert.Equal("Title * with [brackets]", Assert.Single(parsed.Blocks[0].Spans!).Text);
        Assert.Equal("## not a heading ", parsed.Blocks[1].Spans![0].Text);
        Assert.Equal("/ar/الخدمات", parsed.Blocks[1].Spans![1].Link!.Path);
        Assert.Equal("1. not a list", Assert.Single(parsed.Blocks[2].Spans!).Text);
        Assert.Equal("Alt [x]", parsed.Blocks[4].Alt);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))", "Cms.Body.InvalidLink")]
    [InlineData("[x](//evil.example)", "Cms.Body.InvalidLink")]
    [InlineData("![alt](media:not-a-guid)", "Cms.Body.InvalidImage")]
    [InlineData("[[cta:nope]]", "Cms.Body.InvalidCta")]
    public void Invalid_markup_is_reported_with_its_line(string text, string error) =>
        Assert.Equal(error, Assert.Single(BlockTextFormat.Parse("Intro\n\n" + text).Errors).ResourceKey);

    [Fact]
    public void Html_is_kept_as_plain_text()
    {
        var result = BlockTextFormat.Parse("<img src=x onerror=alert(1)>");

        Assert.Equal("<img src=x onerror=alert(1)>", Assert.Single(Assert.Single(result.Blocks).Spans!).Text);
    }

    [Fact]
    public void Faqs_round_trip_and_require_a_question_marker()
    {
        var faqs = new List<FaqDto> { new("What is it?", "An answer."), new("Second?", "Another.") };

        var parsed = BlockTextFormat.ParseFaqs(BlockTextFormat.FormatFaqs(faqs));

        Assert.Empty(parsed.Errors);
        Assert.Equal(faqs, parsed.Faqs);
        Assert.Single(BlockTextFormat.ParseFaqs("No marker\nanswer").Errors);
    }
}
