using System.Text.Json.Nodes;
using Application.Features.Commands.StructuredData;
using Domain.Entities.StructuredData;
using Shouldly;

namespace Cms.Tests.StructuredData;

/// <summary>
/// Covers <see cref="SchemaNodeFactory.BuildArticle"/> — lifts an Article node
/// straight out of the page's own Article block content, the same way
/// <see cref="SchemaNodeFactory.BuildFaq"/> lifts questions from the FAQ block. An
/// editor who already wrote a headline and byline should not have to retype them
/// into the Structured Data builder's form.
/// </summary>
public sealed class SchemaNodeFactoryArticleTests
{
    private const string PageUrl = "https://example.com/blog/hello";

    [Fact]
    public void A_filled_in_article_block_produces_a_complete_node()
    {
        string html = """
            <article data-elevare-article>
                <header>
                    <h2 data-elevare-article-headline>Baslik Buraya</h2>
                    <p>Yazar: <span data-elevare-article-author>Ada Lovelace</span>
                       <time data-elevare-article-date datetime="2026-03-05T00:00:00Z">5 Mart 2026</time></p>
                </header>
                <div data-elevare-article-body>
                    <p>Acilis paragrafi burada, ozet olarak kullanilacak.</p>
                    <p>Ikinci paragraf, ozete dahil edilmemeli.</p>
                </div>
            </article>
            """;

        JsonObject? node = SchemaNodeFactory.BuildArticle(html, PageUrl);

        node.ShouldNotBeNull();
        node[SchemaGraph.TypeKey]!.GetValue<string>().ShouldBe(SchemaCatalog.Article.Type);
        node[SchemaGraph.IdKey]!.GetValue<string>().ShouldBe($"{PageUrl}#article");
        node["headline"]!.GetValue<string>().ShouldBe("Baslik Buraya");
        node["author"]!["name"]!.GetValue<string>().ShouldBe("Ada Lovelace");
        node["datePublished"]!.GetValue<string>().ShouldStartWith("2026-03-05");
        node["description"]!.GetValue<string>().ShouldBe("Acilis paragrafi burada, ozet olarak kullanilacak.");
        node["description"]!.GetValue<string>().ShouldNotContain("Ikinci paragraf");
    }

    [Fact]
    public void A_page_with_no_article_block_returns_null()
    {
        SchemaNodeFactory.BuildArticle("<h1>Sadece bir baslik</h1>", PageUrl).ShouldBeNull();
    }

    [Fact]
    public void An_article_block_with_no_headline_filled_in_returns_null()
    {
        string html = """
            <article data-elevare-article>
                <h2 data-elevare-article-headline></h2>
            </article>
            """;

        SchemaNodeFactory.BuildArticle(html, PageUrl).ShouldBeNull();
    }

    [Fact]
    public void Missing_author_and_date_are_simply_omitted()
    {
        string html = """
            <article data-elevare-article>
                <h2 data-elevare-article-headline>Sadece baslik</h2>
            </article>
            """;

        JsonObject? node = SchemaNodeFactory.BuildArticle(html, PageUrl);

        node.ShouldNotBeNull();
        node["headline"]!.GetValue<string>().ShouldBe("Sadece baslik");
        node.ContainsKey("author").ShouldBeFalse();
        node.ContainsKey("datePublished").ShouldBeFalse();
        node.ContainsKey("description").ShouldBeFalse();
    }

    [Fact]
    public void A_long_opening_paragraph_is_truncated_for_the_description()
    {
        string longText = new string('a', 400);
        string html = $"""
            <article data-elevare-article>
                <h2 data-elevare-article-headline>Baslik</h2>
                <div data-elevare-article-body><p>{longText}</p></div>
            </article>
            """;

        JsonObject? node = SchemaNodeFactory.BuildArticle(html, PageUrl);

        node.ShouldNotBeNull();
        string description = node["description"]!.GetValue<string>();
        description.Length.ShouldBeLessThan(longText.Length);
        description.ShouldEndWith("…");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Null_or_blank_html_returns_null(string? html)
    {
        SchemaNodeFactory.BuildArticle(html, PageUrl).ShouldBeNull();
    }
}
