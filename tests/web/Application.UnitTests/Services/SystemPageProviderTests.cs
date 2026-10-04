using Application.Services;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using Shouldly;
using SharedKernel.Concrete;

namespace Application.UnitTests.Services;

public sealed class SystemPageProviderTests
{
    private const string Turkish = "tr";
    private const string English = "en";

    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();

    public SystemPageProviderTests()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        db.Languages.AddRange(
            new PublicLanguage { Id = 1, TwoLetterCode = Turkish, IsActive = true, IsPublished = true, IsDefault = true },
            new PublicLanguage { Id = 2, TwoLetterCode = English, IsActive = true, IsPublished = true });

        db.PageInfos.AddRange(
            new PublicPage
            {
                Id = 1,
                Slug = "404",
                FullSlug = "404",
                LanguageId = 1,
                PageStatus = PublicPageStatus.Published,
                IsActive = true,
                SeoTitle = "Bulunamadı",
                Content = new PublicPageContent { Id = 1, PageInfoId = 1, GjsHtml = "<h1>ÖZEL 404</h1>", GjsCss = "h1{color:red}" }
            },
            // The English counterpart carries the language prefix in its FullSlug,
            // exactly as PageInfo.ComputeFullSlug writes it on the CMS side.
            new PublicPage
            {
                Id = 3,
                Slug = "404",
                FullSlug = "en/404",
                LanguageId = 2,
                PageStatus = PublicPageStatus.Published,
                IsActive = true,
                SeoTitle = "Not Found",
                Content = new PublicPageContent { Id = 3, PageInfoId = 3, GjsHtml = "<h1>CUSTOM 404</h1>" }
            },
            new PublicPage
            {
                Id = 2,
                Slug = "500",
                FullSlug = "500",
                LanguageId = 1,
                PageStatus = PublicPageStatus.Draft, // not published → must not be used
                IsActive = true,
                SeoTitle = "Hata",
                Content = new PublicPageContent { Id = 2, PageInfoId = 2, GjsHtml = "<h1>TASLAK 500</h1>" }
            },
            // Turkish-only: used to prove the fallback when a translation is missing.
            new PublicPage
            {
                Id = 4,
                Slug = "maintenance",
                FullSlug = "maintenance",
                LanguageId = 1,
                PageStatus = PublicPageStatus.Published,
                IsActive = true,
                SeoTitle = "Bakım",
                Content = new PublicPageContent { Id = 4, PageInfoId = 4, GjsHtml = "<h1>BAKIMDAYIZ</h1>" }
            });

        db.SaveChanges();
    }

    private SystemPageProvider CreateProvider(PublicReadDbContext db) => new(db, new TemplateResolutionService(db));

    /// <summary>
    /// Asserts the lookup itself succeeded and returns its value, so each test can keep
    /// asserting on "content or null" — the distinction the provider is really about.
    /// </summary>
    private async Task<SystemPageContent?> TryGetAsync(PublicReadDbContext db, string slug, string? languageCode = Turkish)
    {
        Result<SystemPageContent?> result = await CreateProvider(db).TryGetAsync(slug, languageCode, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    [Fact]
    public async Task Published_system_page_is_returned_with_its_css()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        SystemPageContent? content = await TryGetAsync(db, "404");

        content.ShouldNotBeNull();
        content.Html.ShouldContain("ÖZEL 404");
        content.Css.ShouldContain("h1{color:red}");
    }

    [Fact]
    public async Task Draft_system_page_is_ignored()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        SystemPageContent? content = await TryGetAsync(db, "500");

        content.ShouldBeNull();
    }

    [Fact]
    public async Task Missing_system_page_returns_null()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        SystemPageContent? content = await TryGetAsync(db, "no-such-system-page");

        content.ShouldBeNull();
    }

    [Fact]
    public async Task A_non_default_language_gets_its_own_translation()
    {
        // The bug this covers: the lookup used to match on the bare slug only, so a
        // dead link under /en/ answered with the TURKISH 404 and the English one —
        // which the editor had published — was never reachable at all.
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        SystemPageContent? content = await TryGetAsync(db, "404", English);

        content.ShouldNotBeNull();
        content.Html.ShouldContain("CUSTOM 404");
    }

    [Fact]
    public async Task An_untranslated_system_page_falls_back_to_the_default_language()
    {
        // Wrong language beats no page at all: the built-in static fallback is in a
        // language nobody chose, while this one at least came from the CMS.
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        SystemPageContent? content = await TryGetAsync(db, "maintenance", English);

        content.ShouldNotBeNull();
        content.Html.ShouldContain("BAKIMDAYIZ");
    }

    [Fact]
    public async Task The_default_language_never_picks_up_a_prefixed_page()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        SystemPageContent? content = await TryGetAsync(db, "404", Turkish);

        content.ShouldNotBeNull();
        content.Html.ShouldContain("ÖZEL 404");
        content.Html.ShouldNotContain("CUSTOM 404");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task An_unknown_language_resolves_the_default_language_page(string? languageCode)
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        SystemPageContent? content = await TryGetAsync(db, "404", languageCode);

        content.ShouldNotBeNull();
        content.Html.ShouldContain("ÖZEL 404");
    }
}
