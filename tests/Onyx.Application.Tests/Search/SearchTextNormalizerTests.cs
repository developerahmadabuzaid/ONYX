using Onyx.Application.Search;
using Onyx.Domain.Content;

namespace Onyx.Application.Tests.Search;

public sealed class SearchTextNormalizerTests
{
    [Theory]
    [InlineData("الذَّكَاءُ الاصْطِنَاعِيُّ", "الذكاء الاصطناعي")]          // tashkeel
    [InlineData("الذكـــاء", "الذكاء")]                                   // tatweel
    [InlineData("أتمتة إدارة آلية ٱلعمل", "اتمته اداره اليه العمل")]       // alef forms and ta marbuta
    [InlineData("مستشفى", "مستشفي")]                                      // alef maqsura
    [InlineData("مؤسسة هيئة", "موسسه هييه")]                              // hamza on waw and ya
    [InlineData("عام ٢٠٢٦ و۲۰۲۷", "عام 2026 و2027")]                      // Arabic-Indic and Persian digits
    [InlineData("AI-Powered, Automation!", "ai powered automation")]      // punctuation and case
    [InlineData("ＴｉｒｅＺｏｎｅ", "tirezone")]                            // NFKC (full-width)
    [InlineData("  many   spaces\t\nhere ", "many spaces here")]
    [InlineData(null, "")]
    public void Arabic_and_english_text_are_normalized_the_same_way_for_index_and_query(string? input, string expected) =>
        Assert.Equal(expected, SearchTextNormalizer.Normalize(input));

    [Theory]
    [InlineData("حلول الذكاء", "الذكاء")]
    [InlineData("tire zo", "zo")]
    [InlineData("a", null)]
    [InlineData("crm x", null)]
    public void Only_a_plain_last_word_becomes_a_prefix_term(string normalized, string? expected) =>
        Assert.Equal(expected, SiteSearchQueryService.PrefixTerm(normalized));

    [Fact]
    public void Query_syntax_never_survives_normalization_into_the_prefix_term()
    {
        var normalized = SearchTextNormalizer.Normalize("'; DROP TABLE search_documents; -- :* | & !");

        Assert.DoesNotContain("'", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain(";", normalized, StringComparison.Ordinal);
        Assert.Equal("documents", SiteSearchQueryService.PrefixTerm(SearchTextNormalizer.Normalize("search_documents")));
        Assert.All(normalized.Split(' '), word => Assert.True(word.All(char.IsLetterOrDigit)));
    }

    [Theory]
    [InlineData("\"Medical Record\" -Jordan", "medical record", "\"medical record\" -jordan", "jordan")]
    [InlineData("الذَّكَاءُ -\"آلية العمل\" تجربة", "الذكاء تجربه", "ذكاء -\"اليه العمل\" تجربه", "اليه العمل")]
    [InlineData("'; DROP TABLE x; --", "drop table x", "drop table x", "")]
    [InlineData(":* | & ! ( ) - \"", "", "", "")]
    [InlineData(null, "", "", "")]
    public void Queries_keep_only_phrases_and_exclusions_as_syntax(string? input, string words, string webQuery, string excluded) =>
        Assert.Equal(new ParsedSearchQuery(words, webQuery, excluded), SearchTextNormalizer.ParseQuery(input));

    [Theory]
    [InlineData("والذكاء الاصطناعي", "والذكاء الاصطناعي ذكاء اصطناعي")]
    [InlineData("فالأتمتة ولد والي", "فالاتمته ولد والي اتمته")]
    [InlineData("Wall automation", "wall automation")]
    public void Index_text_adds_arabic_words_without_conjunction_and_article(string input, string expected) =>
        Assert.Equal(expected, SearchTextNormalizer.ForIndex(input));

    [Theory]
    [InlineData(ContentType.ServicePillar, "Services")]
    [InlineData(ContentType.Service, "Services")]
    [InlineData(ContentType.Industry, "Industries")]
    [InlineData(ContentType.Project, "Work")]
    [InlineData(ContentType.CaseStudy, "Work")]
    [InlineData(ContentType.Insight, "Insights")]
    [InlineData(ContentType.Topic, "Insights")]
    [InlineData(ContentType.GlossaryTerm, "Glossary")]
    [InlineData(ContentType.Integration, "Integrations")]
    [InlineData(ContentType.Page, "Pages")]
    public void Content_types_map_to_result_groups(ContentType type, string group) => Assert.Equal(group, SearchGroups.For(type));
}
