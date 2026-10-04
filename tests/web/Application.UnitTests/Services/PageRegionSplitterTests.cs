using Application.Services;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Covers <see cref="PageRegionSplitter"/> — the pass that lifts a page's top-level
/// header and footer out of its content so the layout can place them outside
/// <c>&lt;main&gt;</c>, giving the document a real banner/main/contentinfo outline.
/// </summary>
public sealed class PageRegionSplitterTests
{
    private static Task<PageRegions> SplitAsync(string? html)
        => PageRegionSplitter.SplitAsync(html, CancellationToken.None);

    [Fact]
    public async Task A_top_level_header_and_footer_are_lifted_out_of_main()
    {
        PageRegions regions = await SplitAsync(
            "<header>MENU</header><section>ICERIK</section><footer>ALT BILGI</footer>");

        regions.Header.ShouldContain("MENU");
        regions.Footer.ShouldContain("ALT BILGI");
        regions.Main.ShouldContain("ICERIK");
        regions.Main.ShouldNotContain("MENU");
        regions.Main.ShouldNotContain("ALT BILGI");
    }

    /// <summary>
    /// The rule is "direct child", and this is the case that makes it necessary.
    /// The Article block puts its byline inside its own <c>&lt;header&gt;</c>, nested
    /// in <c>&lt;article&gt;</c>. That header belongs to the article, not the site —
    /// hoisting it would tear the byline off the top of the article and staple it to
    /// the site banner.
    /// </summary>
    [Fact]
    public async Task A_header_nested_inside_an_article_stays_where_it_is()
    {
        PageRegions regions = await SplitAsync(
            "<header>SITE MENUSU</header><article><header>YAZAR SATIRI</header><p>Metin</p></article>");

        regions.Header.ShouldContain("SITE MENUSU");
        regions.Header.ShouldNotContain("YAZAR SATIRI");
        regions.Main.ShouldContain("YAZAR SATIRI");
        regions.Main.ShouldContain("Metin");
    }

    /// <summary>
    /// Same rule from the other direction: a menu dropped inside a section is not the
    /// page banner, so it is left alone rather than being pulled out of its layout.
    /// </summary>
    [Fact]
    public async Task A_header_nested_inside_a_section_is_not_hoisted()
    {
        PageRegions regions = await SplitAsync("<section><header>IC MENU</header></section>");

        regions.Header.ShouldBeEmpty();
        regions.Main.ShouldContain("IC MENU");
    }

    /// <summary>
    /// Extras are kept, in order. Dropping them would silently delete content someone
    /// put on the page deliberately, which is worse than an unusual outline.
    /// </summary>
    [Fact]
    public async Task Several_top_level_headers_are_all_kept_in_document_order()
    {
        PageRegions regions = await SplitAsync("<header>BIR</header><p>x</p><header>IKI</header>");

        regions.Header.IndexOf("BIR", StringComparison.Ordinal)
            .ShouldBeLessThan(regions.Header.IndexOf("IKI", StringComparison.Ordinal));
        regions.Main.ShouldContain("x");
    }

    [Fact]
    public async Task A_page_with_neither_leaves_both_regions_empty_and_main_intact()
    {
        PageRegions regions = await SplitAsync("<section>SADECE ICERIK</section>");

        regions.Header.ShouldBeEmpty();
        regions.Footer.ShouldBeEmpty();
        regions.Main.ShouldContain("SADECE ICERIK");
    }

    /// <summary>Nothing to split is not an error — it passes straight through.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Empty_input_passes_through(string? html)
    {
        PageRegions regions = await SplitAsync(html);

        regions.Header.ShouldBeEmpty();
        regions.Footer.ShouldBeEmpty();
        regions.Main.ShouldBe(html ?? string.Empty);
    }
}
