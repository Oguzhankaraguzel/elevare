using System.Text.Json.Nodes;
using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.StructuredData;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using Shouldly;

namespace Cms.Tests.StructuredData;

/// <summary>
/// The draft's BreadcrumbList and WebSite on a two-language site, as found on the
/// live site: an English page's trail began at "/" (the Turkish homepage) before
/// "/en", a Turkish page under the homepage had two home crumbs, and the WebSite at
/// "/" said it was in English whenever the page was.
/// </summary>
public sealed class SchemaNodeFactoryLanguageTests
{
    private const string BaseUrl = "https://example.com";

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private readonly Language _turkish = new() { Id = 1, NameInNative = "Türkçe", NameInEnglish = "Turkish", TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true };
    private readonly Language _english = new() { Id = 2, NameInNative = "English", NameInEnglish = "English", TwoLetterCode = "en", IsActive = true, IsPublished = true };

    private ApplicationDbContext CreateDb() => new(_options, new TestUserContext());

    private async Task<Dictionary<string, PageInfo>> SeedAsync()
    {
        using ApplicationDbContext db = CreateDb();
        db.Languages.AddRange(_turkish, _english);

        Dictionary<string, PageInfo> pages = [];
        async Task<PageInfo> Add(string key, Language language, string slug, string fullSlug, PageInfo? parent, string title)
        {
            var page = new PageInfo
            {
                Slug = slug, FullSlug = fullSlug, LanguageId = language.Id, ParentPageId = parent?.Id,
                PageStatus = PageStatus.Published, IsActive = true,
                SeoMeta = new SeoMeta { Title = title }, Content = new PageContent(),
            };
            db.PageInfos.Add(page);
            await db.SaveChangesAsync(CancellationToken.None);
            pages[key] = page;
            return page;
        }

        PageInfo trHome = await Add("trHome", _turkish, "home", "", null, "Ana Sayfa");
        PageInfo makaleler = await Add("makaleler", _turkish, "makaleler", "makaleler", trHome, "Makaleler");
        await Add("bit", _turkish, "bit", "makaleler/bit", makaleler, "Bit İşlemleri");
        await Add("arama", _turkish, "arama", "arama", null, "Arama");
        PageInfo enHome = await Add("enHome", _english, "home", "en", null, "Home");
        PageInfo articles = await Add("articles", _english, "articles", "en/articles", enHome, "Articles");
        await Add("bits", _english, "bits", "en/articles/bits", articles, "Bitwise Operations");
        await Add("search", _english, "search", "en/search", null, "Search");
        return pages;
    }

    private async Task<List<(string Name, string Url)>> TrailAsync(PageInfo page)
    {
        using ApplicationDbContext db = CreateDb();
        JsonObject? node = await SchemaNodeFactory.BuildBreadcrumbAsync(db, page, BaseUrl, [], CancellationToken.None);
        node.ShouldNotBeNull();
        return [.. node["itemListElement"]!.AsArray().Select(i => (
            i!["item"]!["name"]!.GetValue<string>(),
            i["item"]![Domain.Entities.StructuredData.SchemaGraph.IdKey]!.GetValue<string>()))];
    }

    [Fact]
    public async Task An_english_trail_starts_at_the_english_homepage_once()
    {
        Dictionary<string, PageInfo> pages = await SeedAsync();

        (await TrailAsync(pages["bits"])).ShouldBe([
            ("Home", BaseUrl + "/en"),
            ("Articles", BaseUrl + "/en/articles"),
            ("Bitwise Operations", BaseUrl + "/en/articles/bits"),
        ]);
    }

    [Fact]
    public async Task A_page_under_the_homepage_has_one_home_crumb()
    {
        Dictionary<string, PageInfo> pages = await SeedAsync();

        (await TrailAsync(pages["bit"])).ShouldBe([
            ("Ana Sayfa", BaseUrl + "/"),
            ("Makaleler", BaseUrl + "/makaleler"),
            ("Bit İşlemleri", BaseUrl + "/makaleler/bit"),
        ]);
    }

    [Fact]
    public async Task The_website_is_the_same_whole_site_on_every_page()
    {
        await SeedAsync();
        using ApplicationDbContext db = CreateDb();

        JsonObject site = await SchemaNodeFactory.BuildWebSiteAsync(db, [], BaseUrl, [_turkish, _english], CancellationToken.None);

        site["url"]!.GetValue<string>().ShouldBe(BaseUrl);
        site["inLanguage"]!.AsArray().Select(l => l!.GetValue<string>()).ShouldBe(["tr", "en"]);
        site["potentialAction"]!["target"]!.GetValue<string>().ShouldBe(BaseUrl + "/arama?q={search_term}");
    }

    private sealed class TestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
