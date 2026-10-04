using Application.Features.Commands.Pages;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// Every way a page's address gets written down follows it when it moves — the
/// case in point being a new default language, where /en/about becomes /about and
/// /hakkimda becomes /tr/hakkimda — and nothing that merely looks like it does.
/// </summary>
public sealed class SiteAddressRewriterTests
{
    private static readonly Uri Site = new("https://www.example.com");

    // English becomes the default: its pages lose the prefix, Turkish ones gain it.
    private static readonly Dictionary<string, string> Moves = new()
    {
        [""] = "tr",
        ["hakkimda"] = "tr/hakkimda",
        ["en"] = "",
        ["en/about"] = "about",
        ["en/articles"] = "articles",
    };

    private static string Rewrite(string text) => SiteAddressRewriter.Create(Moves, Site)!.Rewrite(text)!;

    [Theory]
    [InlineData("https://www.example.com/en/about", "https://www.example.com/about")]
    [InlineData("https://example.com/en/about", "https://example.com/about")]
    [InlineData("http://www.example.com/en/about", "http://www.example.com/about")]
    [InlineData("HTTPS://WWW.EXAMPLE.COM/en/about", "HTTPS://WWW.EXAMPLE.COM/about")]
    [InlineData("https://www.example.com/en/about/", "https://www.example.com/about")]
    public void Every_absolute_spelling_moves(string before, string after) =>
        Rewrite($"\"{before}\"").ShouldBe($"\"{after}\"");

    [Theory]
    [InlineData("<a href=\"/en/about\">", "<a href=\"/about\">")]
    [InlineData("<a href='/en/about#team'>", "<a href='/about#team'>")]
    [InlineData("<a href=\"/en/articles?page=2\">", "<a href=\"/articles?page=2\">")]
    [InlineData("<a href=\"en/about\">", "<a href=\"about\">")]
    [InlineData("<a href=\"/hakkimda\">", "<a href=\"/tr/hakkimda\">")]
    [InlineData("<a href=\"/en\">", "<a href=\"/\">")]
    [InlineData("{\"href\":\"/en/about\"}", "{\"href\":\"/about\"}")]
    [InlineData("\"content\":\"<a href=\\\"/en/about\\\">\"", "\"content\":\"<a href=\\\"/about\\\">\"")]
    [InlineData("Bkz. /en/about sayfası", "Bkz. /about sayfası")]
    public void Relative_links_move(string before, string after) => Rewrite(before).ShouldBe(after);

    [Theory]
    [InlineData("<a href=\"/en/about-us\">")]
    [InlineData("<a href=\"/en/about/team\">")]
    [InlineData("<a href=\"/english\">")]
    [InlineData("<a href=\"https://other.com/en/about\">")]
    [InlineData("<a href=\"https://www.example.com.evil.com/en/about\">")]
    [InlineData("<img src=\"/uploads/images/en/about.jpg\">")]
    public void Lookalikes_stay(string text) => Rewrite(text).ShouldBe(text);

    [Fact]
    public void The_homepage_moves_where_it_is_a_link_to_the_homepage()
    {
        Rewrite("<a href=\"/\">Ana Sayfa</a>").ShouldBe("<a href=\"/tr\">Ana Sayfa</a>");
        Rewrite("{\"@id\": \"https://www.example.com/\"}").ShouldBe("{\"@id\": \"https://www.example.com/tr\"}");
        Rewrite("{\"href\":\"/#iletisim\"}").ShouldBe("{\"href\":\"/tr#iletisim\"}");
    }

    [Fact]
    public void The_site_root_and_lone_slashes_are_not_the_homepage()
    {
        // The Organization and WebSite nodes name the site, written without a slash.
        Rewrite("{\"@id\": \"https://www.example.com\", \"url\": \"https://www.example.com\"}")
            .ShouldBe("{\"@id\": \"https://www.example.com\", \"url\": \"https://www.example.com\"}");
        // A breadcrumb separator.
        Rewrite("{\"type\":\"textnode\",\"content\":\"/\"}").ShouldBe("{\"type\":\"textnode\",\"content\":\"/\"}");
        Rewrite("<span>/</span>").ShouldBe("<span>/</span>");
    }

    [Fact]
    public void Addresses_move_once_even_when_one_page_takes_another_old_address()
    {
        // "/en" becomes "/" and the old "/" becomes "/tr" in the same pass — the
        // English homepage link must not be carried on to /tr.
        Rewrite("<a href=\"/en\">EN</a><a href=\"/\">TR</a>")
            .ShouldBe("<a href=\"/\">EN</a><a href=\"/tr\">TR</a>");
    }

    [Fact]
    public void Nothing_to_do_when_nothing_moved() =>
        SiteAddressRewriter.Create(new Dictionary<string, string> { ["a"] = "a" }, Site).ShouldBeNull();
}
