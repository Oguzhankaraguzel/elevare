using Application.Services;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Covers <see cref="BreadcrumbResolutionService"/>'s trail, as found on the live
/// site: the block's home crumb and the walk up the page tree both produced the
/// homepage — two home crumbs under the Turkish homepage, and an English page that
/// began at "/" (the Turkish homepage) before "/en".
/// </summary>
public sealed class BreadcrumbResolutionServiceTests
{
    private const string Block =
        "<div data-elevare-breadcrumb data-elevare-breadcrumb-home=\"Home\"><ol>" +
        "<li data-elevare-crumb-template><a href=\"#\" data-elevare-crumb-link>x</a><span data-elevare-crumb-sep>/</span></li>" +
        "</ol></div>";

    private static async Task<PublicReadDbContext> SeedAsync()
    {
        DbContextOptions<PublicReadDbContext> options = TestDbFactory.CreateOptions();
        using (PublicReadDbContext seed = TestDbFactory.Create(options))
        {
            seed.PageInfos.AddRange(
                Page(1, 1, "home", "", null, "Ana Sayfa"),
                Page(2, 1, "makaleler", "makaleler", 1, "Makaleler"),
                Page(3, 1, "bit", "makaleler/bit", 2, "Bit İşlemleri"),
                Page(4, 1, "hakkinda", "hakkinda", null, "Hakkında"),
                Page(10, 2, "home", "en", null, "Home"),
                Page(11, 2, "articles", "en/articles", 10, "Articles"),
                Page(12, 2, "bits", "en/articles/bits", 11, "Bitwise Operations"));
            await seed.SaveChangesAsync(CancellationToken.None);
        }
        return TestDbFactory.Create(options);
    }

    private static PublicPage Page(int id, int languageId, string slug, string fullSlug, int? parentId, string title) => new()
    {
        Id = id, LanguageId = languageId, Slug = slug, FullSlug = fullSlug, ParentPageId = parentId,
        SeoTitle = title, PageStatus = PublicPageStatus.Published, IsActive = true,
    };

    private static async Task<List<(string Label, string? Href)>> CrumbsAsync(int pageId)
    {
        using PublicReadDbContext db = await SeedAsync();
        Result<string?> result = await new BreadcrumbResolutionService(db).ResolveAsync(Block, pageId, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();

        var parser = new AngleSharp.Html.Parser.HtmlParser();
        using AngleSharp.Html.Dom.IHtmlDocument doc = parser.ParseDocument(result.Value!);
        return [.. doc.QuerySelectorAll("[data-elevare-crumb-link]").Select(a => (a.TextContent, a.GetAttribute("href")))];
    }

    [Fact]
    public async Task An_english_page_starts_at_the_english_homepage_once()
    {
        List<(string Label, string? Href)> crumbs = await CrumbsAsync(12);

        crumbs.ShouldBe([("Home", "/en"), ("Articles", "/en/articles"), ("Bitwise Operations", null)]);
    }

    [Fact]
    public async Task A_page_under_the_homepage_has_one_home_crumb()
    {
        List<(string Label, string? Href)> crumbs = await CrumbsAsync(3);

        crumbs.ShouldBe([("Home", "/"), ("Makaleler", "/makaleler"), ("Bit İşlemleri", null)]);
    }

    [Fact]
    public async Task A_top_level_page_leads_home_to_its_own_languages_homepage()
    {
        List<(string Label, string? Href)> crumbs = await CrumbsAsync(4);

        crumbs.ShouldBe([("Home", "/"), ("Hakkında", null)]);
    }

    [Fact]
    public async Task The_homepage_has_no_home_crumb_above_itself()
    {
        List<(string Label, string? Href)> crumbs = await CrumbsAsync(10);

        crumbs.ShouldBe([("Home", null)]);
    }
}
