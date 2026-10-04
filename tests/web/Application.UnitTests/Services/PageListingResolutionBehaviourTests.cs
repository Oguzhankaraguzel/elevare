using Application.Services;
using AngleSharp;
using AngleSharp.Dom;
using Domain.Entities.PublicPages;
using Domain.Entities.PublicTags;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// End to end over <see cref="PageListingResolutionService"/>: what a "Sayfa Listesi"
/// block turns into for a given query string — which pages, in which order, with
/// which links — as found wrong on the live site.
/// </summary>
public sealed class PageListingResolutionBehaviourTests
{
    private const int ListingPageId = 11;
    private const string Cover = "/uploads/images/2026/09/cover.jpg";
    private const string Placeholder = "data:image/svg+xml;base64,AAAA";

    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();

    // The block as the editor ships it, trimmed to what matters here: a search form,
    // tag chips, one card, the "no results" note and the pagination templates.
    private static string Block(string attrs = "", string id = "ilist") => $"""
        <div id="{id}" class="elevare-page-listing" data-elevare-items-per-page="2" {attrs}>
          <form class="elevare-listing-search"><input name="q" aria-label="Sitede ara"><button>Ara</button></form>
          <div class="elevare-listing-tagcloud">
            <a id="iall" data-elevare-tag-all href="#">Tümü</a><a id="ichip" data-elevare-tag-chip-template href="#">Örnek</a>
          </div>
          <div class="elevare-listing-items">
            <a data-elevare-item-template href="#">
              <img data-elevare-field="image" src="{Placeholder}" alt="">
              <span data-elevare-field="date">17.07.2026</span>
              <h3 data-elevare-field="title">Örnek</h3>
              <p data-elevare-field="summary">Örnek özet</p>
              <span data-elevare-field="tags">Etiket</span>
            </a>
            <div data-elevare-empty-template>Sonuç bulunamadı.</div>
          </div>
          <ul class="elevare-listing-pagination">
            <li data-elevare-prev-template><a id="iprev" href="#">«</a></li>
            <li data-elevare-page-link-template><a id="inum" href="#">1</a></li>
            <li data-elevare-next-template><a id="inext" href="#">»</a></li>
          </ul>
        </div>
        """;

    public PageListingResolutionBehaviourTests()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        db.PageInfos.AddRange(
            Page(ListingPageId, "makaleler", parent: null, published: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)),
            Page(8, "makaleler/bit", ListingPageId, new DateTime(2023, 8, 13, 0, 0, 0, DateTimeKind.Utc), title: "C# ile Bit Tabanlı İşlemler",
                description: "AND, OR ve XOR operatörleri.", ogImage: Cover),
            Page(10, "makaleler/aristo", ListingPageId, new DateTime(2023, 9, 12, 0, 0, 0, DateTimeKind.Utc), title: "Nesne Tabanlı Programlama",
                html: """<header><img src="/logo.png"></header><article data-elevare-article><div data-elevare-article-body><p>Aristoteles'in kategorileri ile sınıflar arasındaki şaşırtıcı benzerlik üzerine bir yazı.</p><img src="/uploads/aristo.jpg"></div></article>"""),
            Page(12, "makaleler/taslak", ListingPageId, new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), title: "Taslak Yazı", status: PublicPageStatus.Draft),
            Page(13, "makaleler/eski", ListingPageId, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), title: "Eski Yazı"),
            // For the "every published page" source.
            Page(2, "", null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), title: "Ana Sayfa", slug: "home"),
            Page(4, "404", null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), title: "Bulunamadı", slug: "404"),
            Page(20, "en/articles/bitwise", 21, new DateTime(2023, 8, 13, 0, 0, 0, DateTimeKind.Utc), title: "Bitwise", languageId: 2));
        db.Tags.AddRange(new PublicTag { Id = 1, Name = "csharp", Slug = "csharp" }, new PublicTag { Id = 2, Name = "felsefe", Slug = "felsefe" });
        db.PageInfoTags.AddRange(new PublicPageInfoTag { PageInfoId = 8, TagId = 1 }, new PublicPageInfoTag { PageInfoId = 10, TagId = 2 });
        db.SaveChanges();
    }

    private static PublicPage Page(
        int id, string fullSlug, int? parent, DateTime published, string title = "Makaleler", string description = "",
        string? ogImage = null, string? html = null, PublicPageStatus status = PublicPageStatus.Published,
        string? slug = null, int languageId = 1) => new()
        {
            Id = id, Slug = slug ?? fullSlug.Split('/')[^1], FullSlug = fullSlug, ParentPageId = parent, LanguageId = languageId,
            PageStatus = status, IsActive = true, SeoTitle = title, SeoMetaDescription = description, OgImage = ogImage,
            CreateDate = new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc),
            PublishedAt = DateTime.SpecifyKind(published, DateTimeKind.Utc),
            Content = html is null ? null : new PublicPageContent { Id = id, PageInfoId = id, GjsHtml = html },
        };

    private async Task<(IDocument Html, ListingPaginationInfo Info)> ResolveAsync(
        string query = "", string? block = null, string language = "tr", int currentPageId = ListingPageId)
    {
        Dictionary<string, string?> parsed = query.Length == 0
            ? []
            : query.Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => (string?)Uri.UnescapeDataString(p.Length > 1 ? p[1] : ""));
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        Result<PageListingResolution> result = await new PageListingResolutionService(db).ResolveAsync(
            block ?? Block(), new ListingRequest(currentPageId, "/makaleler", language, parsed), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        IDocument document = await BrowsingContext.New(Configuration.Default).OpenAsync(r => r.Content(result.Value.Html ?? ""));
        return (document, result.Value.Pagination!);
    }

    private static string[] Titles(IDocument html) =>
        [.. html.QuerySelectorAll(".elevare-listing-items [data-elevare-field=title]").Select(e => e.TextContent)];

    [Fact]
    public async Task Drafts_are_not_listed_and_the_newest_by_publish_date_comes_first()
    {
        (IDocument html, ListingPaginationInfo info) = await ResolveAsync();

        Titles(html).ShouldBe(["Nesne Tabanlı Programlama", "C# ile Bit Tabanlı İşlemler"]);
        info.TotalPages.ShouldBe(2);
        info.ItemPaths.ShouldBe(["/makaleler/aristo", "/makaleler/bit"]);
    }

    [Fact]
    public async Task Cards_show_the_publish_date_in_the_pages_language()
    {
        (IDocument tr, _) = await ResolveAsync();
        (IDocument en, _) = await ResolveAsync(language: "en");

        tr.QuerySelectorAll("[data-elevare-field=date]").Select(e => e.TextContent).ShouldBe(["12 Eylül 2023", "13 Ağustos 2023"]);
        en.QuerySelector("[data-elevare-field=date]")!.TextContent.ShouldBe("September 12, 2023");
    }

    [Fact]
    public async Task A_card_without_its_own_image_or_description_takes_them_from_the_content()
    {
        (IDocument html, _) = await ResolveAsync();
        IElement aristo = html.QuerySelectorAll(".elevare-listing-items > a")[0];

        aristo.QuerySelector("img")!.GetAttribute("src").ShouldBe("/uploads/aristo.jpg");
        aristo.QuerySelector("[data-elevare-field=summary]")!.TextContent.ShouldStartWith("Aristoteles'in kategorileri");
    }

    [Fact]
    public async Task Nothing_is_left_showing_the_templates_sample_or_its_placeholder_picture()
    {
        (IDocument html, _) = await ResolveAsync("page=2");
        IElement old = html.QuerySelector(".elevare-listing-items > a")!;

        old.QuerySelector("img").ShouldBeNull();
        old.QuerySelector("[data-elevare-field=summary]").ShouldBeNull();
        old.QuerySelector("[data-elevare-field=tags]").ShouldBeNull();
        html.QuerySelector("[data-elevare-item-template]").ShouldBeNull();
    }

    [Fact]
    public async Task Search_ignores_case_and_looks_at_the_description_too()
    {
        Titles((await ResolveAsync("q=bit")).Html).ShouldBe(["C# ile Bit Tabanlı İşlemler"]);
        Titles((await ResolveAsync("q=xor")).Html).ShouldBe(["C# ile Bit Tabanlı İşlemler"]);
        (await ResolveAsync("q=bit")).Info.IsSearch.ShouldBeTrue();
    }

    [Fact]
    public async Task A_page_past_the_last_is_reported_out_of_range()
    {
        (await ResolveAsync("page=99")).Info.IsOutOfRange.ShouldBeTrue();
        (await ResolveAsync("page=2")).Info.IsOutOfRange.ShouldBeFalse();
        (await ResolveAsync("q=hicbirsey")).Info.IsOutOfRange.ShouldBeFalse(); // an empty page 1 is a real answer
    }

    [Fact]
    public async Task A_page_number_that_is_not_one_is_page_one()
    {
        (await ResolveAsync("page=abc")).Info.Page.ShouldBe(1);
        (await ResolveAsync("page=-3")).Info.Page.ShouldBe(1);
    }

    [Fact]
    public async Task Page_one_is_the_listings_own_address_and_numbers_are_links_with_the_right_look()
    {
        (IDocument html, _) = await ResolveAsync("page=2");
        List<IElement> numbers = [.. html.QuerySelectorAll(".elevare-listing-pagination li a")];

        numbers.Select(a => a.GetAttribute("href")).ShouldBe(["/makaleler", "/makaleler", "/makaleler?page=2"]);
        numbers[0].GetAttribute("rel").ShouldBe("prev");
        numbers[0].GetAttribute("aria-label").ShouldBe("Önceki sayfa");
        // Only the current page keeps the highlighted look; the others borrow the arrow's.
        numbers[1].Id.ShouldBe("iprev");
        numbers[2].Id.ShouldBe("inum");
        numbers[2].GetAttribute("aria-current").ShouldBe("page");
        html.QuerySelector("nav[aria-label='Sayfalama'] > ul.elevare-listing-pagination").ShouldNotBeNull();
    }

    [Fact]
    public async Task Parameters_that_are_not_the_listings_own_go_nowhere()
    {
        (IDocument html, _) = await ResolveAsync("tag=csharp&q=bit");
        (IDocument tracked, _) = await ResolveAsync("utm_source=x&fbclid=abc&page=2");

        tracked.QuerySelectorAll("input[type=hidden]").ShouldBeEmpty();
        tracked.QuerySelectorAll("a[href*='utm'], a[href*='fbclid']").ShouldBeEmpty();
        html.QuerySelectorAll("input[type=hidden]").Select(i => i.GetAttribute("name")).ShouldBe(["tag"]);
    }

    [Fact]
    public async Task The_search_box_says_it_searches_this_list()
    {
        (IDocument html, _) = await ResolveAsync();

        html.QuerySelector(".elevare-listing-search input[name=q]")!.GetAttribute("aria-label").ShouldBe("Bu listede ara");
    }

    [Fact]
    public async Task All_undoes_a_blocks_default_tag()
    {
        string block = Block("data-elevare-default-tag=\"csharp\"");

        Titles((await ResolveAsync(block: block)).Html).ShouldBe(["C# ile Bit Tabanlı İşlemler"]);
        (IDocument all, ListingPaginationInfo info) = await ResolveAsync("tag=", block);
        Titles(all).Length.ShouldBe(2);
        info.TotalPages.ShouldBe(2);
        all.QuerySelector("[data-elevare-tag-all]")!.GetAttribute("href").ShouldBe("/makaleler?tag=");
    }

    [Fact]
    public async Task Every_published_page_means_this_language_without_system_pages_or_the_listing_itself()
    {
        string block = Block("data-elevare-source=\"all\"").Replace("data-elevare-items-per-page=\"2\"", "data-elevare-items-per-page=\"50\"", StringComparison.Ordinal);

        string[] titles = Titles((await ResolveAsync(block: block)).Html);

        titles.ShouldBe(["Nesne Tabanlı Programlama", "C# ile Bit Tabanlı İşlemler", "Eski Yazı"]);
    }

    [Fact]
    public async Task A_second_listing_on_the_page_has_its_own_parameters()
    {
        string both = Block() + Block(id: "iother");

        (IDocument html, ListingPaginationInfo primary) = await ResolveAsync("page-iother=2", both);
        IElement[] listings = [.. html.QuerySelectorAll(".elevare-page-listing")];

        primary.Page.ShouldBe(1);
        listings[0].QuerySelectorAll("[data-elevare-field=title]")[0].TextContent.ShouldBe("Nesne Tabanlı Programlama");
        listings[1].QuerySelectorAll("[data-elevare-field=title]")[0].TextContent.ShouldBe("Eski Yazı");
        listings[1].QuerySelector("input[name='q-iother']").ShouldNotBeNull();
        // Paging the first keeps the second where it is.
        listings[0].QuerySelector("a[rel=next]")!.GetAttribute("href").ShouldBe("/makaleler?page=2&page-iother=2");
    }

    [Fact]
    public async Task Positions_continue_across_pages()
    {
        (await ResolveAsync("page=2")).Info.FirstPosition.ShouldBe(3);
    }

    [Theory]
    [InlineData("page", true)]
    [InlineData("tag", true)]
    [InlineData("q", true)]
    [InlineData("page-ilist2", true)]
    [InlineData("utm_source", false)]
    [InlineData("pages", false)]
    [InlineData("page-", false)]
    [InlineData("q-a b", false)]
    public void Only_listing_parameters_are_recognised(string key, bool expected)
    {
        ListingQuery.IsListingKey(key).ShouldBe(expected);
    }

    [Fact]
    public void The_cache_key_ignores_everything_else_and_the_order()
    {
        string a = ListingQuery.CacheKey([new("utm_source", "x"), new("q", "bit"), new("page", "2")]);
        string b = ListingQuery.CacheKey([new("page", "2"), new("q", "bit"), new("fbclid", "y")]);

        a.ShouldBe(b);
        a.ShouldBe("page=2&q=bit");
    }

    [Fact]
    public async Task Siblings_are_the_other_live_pages_under_the_same_parent()
    {
        string block = Block("data-elevare-source=\"siblings\"").Replace("data-elevare-items-per-page=\"2\"", "data-elevare-items-per-page=\"10\"", StringComparison.Ordinal);

        string[] titles = Titles((await ResolveAsync(block: block, currentPageId: 8)).Html);

        titles.ShouldBe(["Nesne Tabanlı Programlama", "Eski Yazı"]);
    }

    [Fact]
    public async Task Related_keeps_only_pages_sharing_a_tag()
    {
        string block = Block("data-elevare-source=\"all\" data-elevare-related-tags=\"true\"");

        // Page 8 is tagged "csharp"; nothing else is, so nothing is related to it.
        Titles((await ResolveAsync(block: block, currentPageId: 8)).Html).ShouldBeEmpty();
        // The listing page itself has no tags: nothing is related to it either.
        Titles((await ResolveAsync(block: block)).Html).ShouldBeEmpty();
    }
}
