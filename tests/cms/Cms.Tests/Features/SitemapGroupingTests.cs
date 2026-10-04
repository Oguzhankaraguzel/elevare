using Domain.Entities.Languages;
using Domain.Entities.PageInfos;
using Infrastructure.Sitemap;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// Which pages the sitemaps list and where. Every language version is its own entry
/// — the English pages used to appear only as alternates of the Turkish ones, so
/// half the site was never submitted — and the sitemaps follow what the site serves:
/// a published page under a draft parent is still live at its address.
/// </summary>
public sealed class SitemapGroupingTests
{
    private static readonly Language Turkish = new() { Id = 1, NameInNative = "Türkçe", NameInEnglish = "Turkish", TwoLetterCode = "tr", IsDefault = true, IsPublished = true };
    private static readonly Language English = new() { Id = 2, NameInNative = "English", NameInEnglish = "English", TwoLetterCode = "en", IsPublished = true };

    private readonly List<PageInfo> _pages = [];

    private void Add(int id, Language language, string slug, string fullSlug, int? parent = null, int? group = null,
        PageStatus status = PageStatus.Published, bool noIndex = false)
    {
        var page = new PageInfo
        {
            Id = id, Slug = slug, FullSlug = fullSlug, ParentPageId = parent, PageGroupId = group,
            Language = language, LanguageId = language.Id, PageStatus = status,
            SeoMeta = new SeoMeta { Title = slug, NoIndex = noIndex },
        };
        _pages.Add(page);
    }

    // tr: / → makaleler → csharp, aristo ; en: /en → articles → bitwise
    private void SeedSite()
    {
        Add(1, Turkish, "home", "", group: 10);
        Add(2, Turkish, "makaleler", "makaleler", parent: 1, group: 20);
        Add(3, Turkish, "csharp", "makaleler/csharp", parent: 2, group: 30);
        Add(4, Turkish, "aristo", "makaleler/aristo", parent: 2);
        Add(5, English, "home", "en", group: 10);
        Add(6, English, "articles", "en/articles", parent: 5, group: 20);
        Add(7, English, "bitwise", "en/articles/bitwise", parent: 6, group: 30);
    }

    private Dictionary<string, string[]> Group() =>
        SitemapJob.GroupIntoSitemaps(_pages, "tr").ToDictionary(s => s.Key, s => s.Pages.Select(p => p.FullSlug).ToArray());

    private string Xml(string key) =>
        SitemapJob.BuildUrlSetXml("https://example.com", SitemapJob.GroupIntoSitemaps(_pages, "tr").Single(s => s.Key == key).Pages, _pages, "tr");

    [Fact]
    public void Every_language_version_is_an_entry_in_its_section()
    {
        SeedSite();

        Dictionary<string, string[]> sitemaps = Group();

        sitemaps.Keys.ShouldBe([SitemapJob.MainKey, "makaleler"]);
        sitemaps[SitemapJob.MainKey].ShouldBe(["", "en"]);
        sitemaps["makaleler"].ShouldBe(["makaleler", "en/articles", "makaleler/aristo", "makaleler/csharp", "en/articles/bitwise"]);
    }

    [Fact]
    public void A_page_under_a_draft_parent_is_still_listed()
    {
        SeedSite();
        _pages.Single(p => p.Id == 2).PageStatus = PageStatus.Draft;

        Group()["makaleler"].ShouldBe(["en/articles", "makaleler/aristo", "makaleler/csharp", "en/articles/bitwise"]);
    }

    [Fact]
    public void Drafts_noindex_pages_and_unreleased_languages_are_left_out()
    {
        SeedSite();
        _pages.Single(p => p.Id == 3).PageStatus = PageStatus.Draft;
        _pages.Single(p => p.Id == 4).SeoMeta.NoIndex = true;
        Add(8, new Language { Id = 3, NameInNative = "Deutsch", NameInEnglish = "German", TwoLetterCode = "de" }, "home", "de", group: 10);

        Dictionary<string, string[]> sitemaps = Group();

        sitemaps[SitemapJob.MainKey].ShouldBe(["", "en"]);
        sitemaps["makaleler"].ShouldBe(["makaleler", "en/articles", "en/articles/bitwise"]);
    }

    [Fact]
    public void A_translation_without_a_default_language_version_goes_to_main()
    {
        SeedSite();
        Add(9, English, "about", "en/about", parent: 5);

        Group()[SitemapJob.MainKey].ShouldBe(["", "en", "en/about"]);
    }

    [Fact]
    public void A_translation_stays_in_its_section_while_the_default_version_is_a_draft()
    {
        SeedSite();
        Add(9, English, "about", "en/about", parent: 5, group: 40);
        Add(10, Turkish, "hakkimda", "hakkimda", parent: 1, group: 40, status: PageStatus.Draft);

        Group()["hakkimda"].ShouldBe(["en/about"]);
    }

    [Fact]
    public void Each_entry_names_every_live_version_and_the_default_as_x_default()
    {
        SeedSite();

        string xml = Xml("makaleler");

        xml.ShouldContain("<loc>https://example.com/en/articles/bitwise</loc>");
        // Both entries of the pair carry the full set: tr, en, x-default.
        xml.Split("hreflang=\"x-default\" href=\"https://example.com/makaleler/csharp\"").Length.ShouldBe(3);
        xml.Split("hreflang=\"en\" href=\"https://example.com/en/articles/bitwise\"").Length.ShouldBe(3);
    }

    [Fact]
    public void An_alternate_that_is_not_live_is_not_named()
    {
        SeedSite();
        _pages.Single(p => p.Id == 7).PageStatus = PageStatus.Draft;

        string xml = Xml("makaleler");

        xml.ShouldNotContain("en/articles/bitwise");
        // The Turkish article is now the only live version: no annotation at all.
        xml.ShouldNotContain("https://example.com/makaleler/csharp\" />");
    }
}
