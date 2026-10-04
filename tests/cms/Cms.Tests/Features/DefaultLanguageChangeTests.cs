using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Languages;
using Application.Features.Commands.Languages.CreateLanguage;
using Application.Features.Commands.Languages.UpdateLanguage;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.PageTemplates;
using Domain.Entities.Redirects;
using Domain.Entities.SiteSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// <c>PageInfo.ComputeFullSlug</c> omits the language prefix only for whichever
/// language is currently default (mirrored live, request-time, by the public site's
/// <c>GetPublicPageBySlugQueryHandler</c>) — so flipping which language is default
/// without recomputing every page's stored FullSlug leaves the two sides disagreeing
/// and 404s the entire site. Found while wiring up the public header's language
/// switcher and asking what actually happens when the default language changes.
/// </summary>
public sealed class DefaultLanguageChangeTests
{
    private const int TurkishId = 1;
    private const int EnglishId = 2;

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new DefaultLanguageTestUserContext());

    private readonly SitemapRequests _sitemaps = new();

    private async Task SeedAsync()
    {
        using ApplicationDbContext db = CreateDb();
        db.Languages.Add(new Language
        {
            Id = TurkishId, NameInNative = "Türkçe", NameInEnglish = "Turkish",
            TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true,
        });
        db.Languages.Add(new Language
        {
            Id = EnglishId, NameInNative = "English", NameInEnglish = "English",
            TwoLetterCode = "en", IsDefault = false, IsActive = true, IsPublished = true,
        });

        // FullSlug values reflect the current scheme: "home" is never a literal
        // segment — the default language's homepage is "", any other language's is
        // just its own code.
        var trHome = new PageInfo
        {
            Id = 1, Slug = "home", FullSlug = "", LanguageId = TurkishId,
            PageStatus = PageStatus.Published,
            SeoMeta = new SeoMeta { Title = "Ana Sayfa" },
            Content = new PageContent { GjsHtml = "<h1>Ana Sayfa</h1>" },
        };
        var trCategory = new PageInfo
        {
            Id = 2, Slug = "urunlerimiz", FullSlug = "urunlerimiz", LanguageId = TurkishId, ParentPageId = 1,
            PageStatus = PageStatus.Published,
            SeoMeta = new SeoMeta { Title = "Ürünlerimiz" },
            Content = new PageContent { GjsHtml = "<h1>Ürünlerimiz</h1>" },
        };
        var enHome = new PageInfo
        {
            Id = 3, Slug = "home", FullSlug = "en", LanguageId = EnglishId,
            PageStatus = PageStatus.Published,
            SeoMeta = new SeoMeta { Title = "Home" },
            Content = new PageContent { GjsHtml = "<h1>Home</h1>" },
        };

        db.PageInfos.AddRange(trHome, trCategory, enHome);
        await db.SaveChangesAsync();

        // trCategory needs ParentPage loaded for ComputeFullSlug's walk — reload with
        // the navigation attached, matching how a real request graph would look.
        trCategory.ParentPage = trHome;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Switching_default_language_strips_the_prefix_from_the_new_default_and_adds_it_to_the_old_one()
    {
        await SeedAsync();

        using ApplicationDbContext db = CreateDb();
        Result result = await new UpdateLanguageCommandHandler(db, _sitemaps).Handle(
            new UpdateLanguageCommand(EnglishId, "English", "English", "en", null,
                IsDefault: true, IsRtl: false, IsPublished: true, DisplayOrder: 0, IsActive: true),
            CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        using ApplicationDbContext read = CreateDb();
        (await read.PageInfos.SingleAsync(p => p.Id == 3)).FullSlug.ShouldBe("");
        (await read.PageInfos.SingleAsync(p => p.Id == 1)).FullSlug.ShouldBe("tr");
        (await read.PageInfos.SingleAsync(p => p.Id == 2)).FullSlug.ShouldBe("tr/urunlerimiz");
    }

    [Fact]
    public async Task Switching_default_language_leaves_a_redirect_from_every_published_pages_old_url()
    {
        await SeedAsync();

        using ApplicationDbContext db = CreateDb();
        await new UpdateLanguageCommandHandler(db, _sitemaps).Handle(
            new UpdateLanguageCommand(EnglishId, "English", "English", "en", null,
                IsDefault: true, IsRtl: false, IsPublished: true, DisplayOrder: 0, IsActive: true),
            CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        using ApplicationDbContext read = CreateDb();
        List<Redirect> redirects = await read.Redirects.ToListAsync();

        redirects.ShouldContain(r => r.OldPath == "en" && r.NewPath == "");
        redirects.ShouldContain(r => r.OldPath == "" && r.NewPath == "tr");
        redirects.ShouldContain(r => r.OldPath == "urunlerimiz" && r.NewPath == "tr/urunlerimiz");
    }

    [Fact]
    public async Task A_new_language_created_as_default_still_recomputes_every_existing_pages_slug()
    {
        await SeedAsync();

        using ApplicationDbContext db = CreateDb();
        Result result = await new CreateLanguageCommandHandler(db, _sitemaps).Handle(
            new CreateLanguageCommand("Deutsch", "German", "de", null,
                IsDefault: true, IsRtl: false, IsPublished: true, DisplayOrder: 2),
            CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        using ApplicationDbContext read = CreateDb();
        (await read.PageInfos.SingleAsync(p => p.Id == 1)).FullSlug.ShouldBe("tr");
        (await read.PageInfos.SingleAsync(p => p.Id == 3)).FullSlug.ShouldBe("en");
    }

    [Fact]
    public async Task Saving_a_language_that_is_not_becoming_default_does_not_touch_any_slug()
    {
        await SeedAsync();

        using ApplicationDbContext db = CreateDb();
        Result result = await new UpdateLanguageCommandHandler(db, _sitemaps).Handle(
            new UpdateLanguageCommand(EnglishId, "English (US)", "English", "en", null,
                IsDefault: false, IsRtl: false, IsPublished: true, DisplayOrder: 0, IsActive: true),
            CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        using ApplicationDbContext read = CreateDb();
        (await read.PageInfos.SingleAsync(p => p.Id == 1)).FullSlug.ShouldBe("");
        (await read.PageInfos.SingleAsync(p => p.Id == 3)).FullSlug.ShouldBe("en");
        (await read.Redirects.ToListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_stored_address_of_a_moved_page_follows_it()
    {
        await SeedAsync();
        using (ApplicationDbContext seed = CreateDb())
        {
            seed.SiteSettings.Add(new SiteSetting { Key = "Advanced.PublicSiteBaseUrl", DisplayName = "Site", Value = "https://www.example.com" });
            seed.SiteSettings.Add(new SiteSetting { Key = "Seo.LlmsTxt", DisplayName = "llms", Value = "- [Products](https://example.com/urunlerimiz)" });
            seed.PageTemplates.Add(new PageTemplate { Name = "Menu", GjsHtml = "<a href=\"https://www.example.com/urunlerimiz\">Ürünler</a><a href=\"/en\">EN</a>" });
            seed.Redirects.Add(new Redirect { OldPath = "kampanya", NewPath = "urunlerimiz", Reason = RedirectReason.Manual });
            PageInfo english = await seed.PageInfos.Include(p => p.Content).SingleAsync(p => p.Id == 3);
            english.Content!.GjsHtml = "<a href=\"/en\">Home</a> <a href=\"/urunlerimiz\">TR</a>";
            english.SeoMeta.OgUrl = "https://www.example.com/en";
            english.SeoMeta.StructuredData = "{\"@graph\":[{\"@type\":\"Organization\",\"url\":\"https://www.example.com\"},{\"@type\":\"WebPage\",\"@id\":\"https://www.example.com/en\",\"url\":\"https://www.example.com/en\"}]}";
            await seed.SaveChangesAsync();
        }

        using ApplicationDbContext db = CreateDb();
        Result<LanguageSaveResult> result = await new UpdateLanguageCommandHandler(db, _sitemaps).Handle(
            new UpdateLanguageCommand(EnglishId, "English", "English", "en", null,
                IsDefault: true, IsRtl: false, IsPublished: true, DisplayOrder: 0, IsActive: true),
            CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        result.Value.ShouldBe(new LanguageSaveResult(true, 3, 5));
        _sitemaps.Count.ShouldBe(1);

        using ApplicationDbContext read = CreateDb();
        PageInfo home = await read.PageInfos.Include(p => p.Content).SingleAsync(p => p.Id == 3);
        home.Content!.GjsHtml.ShouldBe("<a href=\"/\">Home</a> <a href=\"/tr/urunlerimiz\">TR</a>");
        home.SeoMeta.OgUrl.ShouldBe("https://www.example.com/");
        home.SeoMeta.StructuredData!.ShouldContain("\"url\":\"https://www.example.com\"}");
        home.SeoMeta.StructuredData!.ShouldContain("\"@id\":\"https://www.example.com/\"");
        (await read.PageTemplates.SingleAsync()).GjsHtml.ShouldBe("<a href=\"https://www.example.com/tr/urunlerimiz\">Ürünler</a><a href=\"/\">EN</a>");
        (await read.SiteSettings.SingleAsync(s => s.Key == "Seo.LlmsTxt")).Value.ShouldBe("- [Products](https://example.com/tr/urunlerimiz)");
        (await read.Redirects.SingleAsync(r => r.OldPath == "kampanya")).NewPath.ShouldBe("tr/urunlerimiz");
    }

    [Fact]
    public async Task A_save_that_keeps_the_default_reports_nothing_moved()
    {
        await SeedAsync();

        using ApplicationDbContext db = CreateDb();
        Result<LanguageSaveResult> result = await new UpdateLanguageCommandHandler(db, _sitemaps).Handle(
            new UpdateLanguageCommand(EnglishId, "English", "English", "en", null,
                IsDefault: false, IsRtl: false, IsPublished: true, DisplayOrder: 0, IsActive: true),
            CancellationToken.None);

        result.Value.ShouldBe(LanguageSaveResult.Unchanged);
        _sitemaps.Count.ShouldBe(0);
    }

    [Fact]
    public async Task An_address_from_an_earlier_rename_keeps_redirecting_and_its_links_move()
    {
        await SeedAsync();
        using (ApplicationDbContext seed = CreateDb())
        {
            // "urunlerimiz" used to be "eski-urunler".
            seed.Redirects.Add(new Redirect { OldPath = "eski-urunler", NewPath = "urunlerimiz", SourcePageId = 2, Reason = RedirectReason.SlugChanged });
            PageInfo home = await seed.PageInfos.Include(p => p.Content).SingleAsync(p => p.Id == 1);
            home.Content!.GjsHtml = "<a href=\"/eski-urunler\">Ürünler</a>";
            await seed.SaveChangesAsync();
        }

        await SwitchDefaultAsync(EnglishId, "en");

        using ApplicationDbContext read = CreateDb();
        List<Redirect> redirects = await read.Redirects.Where(r => r.SourcePageId == 2).ToListAsync();
        redirects.Select(r => r.OldPath).ShouldBe(["eski-urunler", "urunlerimiz"], ignoreOrder: true);
        (await read.PageContents.SingleAsync(c => c.PageInfoId == 1)).GjsHtml.ShouldBe("<a href=\"/tr/urunlerimiz\">Ürünler</a>");
    }

    [Fact]
    public async Task Switching_back_drops_the_rule_on_the_address_a_page_returns_to()
    {
        await SeedAsync();

        await SwitchDefaultAsync(EnglishId, "en");
        await SwitchDefaultAsync(TurkishId, "tr");

        using ApplicationDbContext read = CreateDb();
        (await read.PageInfos.SingleAsync(p => p.Id == 2)).FullSlug.ShouldBe("urunlerimiz");
        List<Redirect> redirects = await read.Redirects.Where(r => r.SourcePageId == 2).ToListAsync();
        redirects.Select(r => r.OldPath).ShouldBe(["tr/urunlerimiz"]);
    }

    private async Task SwitchDefaultAsync(int languageId, string code)
    {
        using ApplicationDbContext db = CreateDb();
        Language language = await db.Languages.AsNoTracking().SingleAsync(l => l.Id == languageId);
        Result<LanguageSaveResult> result = await new UpdateLanguageCommandHandler(db, _sitemaps).Handle(
            new UpdateLanguageCommand(languageId, language.NameInNative, language.NameInEnglish, code, null,
                IsDefault: true, IsRtl: false, IsPublished: true, DisplayOrder: 0, IsActive: true),
            CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private sealed class SitemapRequests : ISitemapRegenerator
    {
        public int Count { get; private set; }
        public void RequestRegeneration() => Count++;
    }

    private sealed class DefaultLanguageTestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
