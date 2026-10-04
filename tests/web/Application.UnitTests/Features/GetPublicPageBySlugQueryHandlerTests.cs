using Application.Features.Queries.Pages.GetPublicPageBySlug;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Features;

/// <summary>
/// End-to-end tests of the public page lookup: language-prefix rules
/// (default language has no prefix, others do), the Published-only gate,
/// and the diagnostic error codes that explain WHY a lookup failed.
/// </summary>
public sealed class GetPublicPageBySlugQueryHandlerTests
{
    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();

    public GetPublicPageBySlugQueryHandlerTests()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        db.Languages.AddRange(
            new PublicLanguage { Id = 1, TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true },
            new PublicLanguage { Id = 2, TwoLetterCode = "en", IsDefault = false, IsActive = true, IsPublished = true });

        db.PageInfos.AddRange(
            NewPage(1, "test", PublicPageStatus.Published, languageId: 1,
                content: new PublicPageContent { Id = 1, PageInfoId = 1, GjsHtml = "<h1>Merhaba</h1>", GjsCss = "h1{color:red}" }),
            NewPage(2, "en/papers", PublicPageStatus.Published, languageId: 2),
            NewPage(3, "taslak-sayfa", PublicPageStatus.Draft, languageId: 1),
            NewPage(4, "arsiv-sayfa", PublicPageStatus.Archived, languageId: 1),
            NewPage(5, "silinmis-sayfa", PublicPageStatus.Published, languageId: 1, isDeleted: true),
            NewPage(6, "pasif-sayfa", PublicPageStatus.Published, languageId: 1, isActive: false),
            // Language group: 7 (tr, default) <-> 8 (en) share pageGroupId 100.
            NewPage(7, "hakkimizda", PublicPageStatus.Published, languageId: 1, pageGroupId: 100),
            NewPage(8, "en/about", PublicPageStatus.Published, languageId: 2, pageGroupId: 100),
            // The homepage itself: "home" is never a real FullSlug segment — the
            // default language's homepage is "" (bare "/"), any other language's is
            // just its own code ("en", not "en/home").
            NewPage(9, "", PublicPageStatus.Published, languageId: 1),
            NewPage(10, "en", PublicPageStatus.Published, languageId: 2));

        db.SaveChanges();
    }

    private static PublicPage NewPage(
        int id, string fullSlug, PublicPageStatus status, int languageId,
        bool isDeleted = false, bool isActive = true, PublicPageContent? content = null, int? pageGroupId = null) => new()
    {
        Id = id,
        Slug = fullSlug[(fullSlug.LastIndexOf('/') + 1)..],
        FullSlug = fullSlug,
        LanguageId = languageId,
        PageGroupId = pageGroupId,
        PageStatus = status,
        IsDeleted = isDeleted,
        IsActive = isActive,
        SeoTitle = $"Title of {fullSlug}",
        Content = content
    };

    private async Task<Result<PublicPageResponse>> HandleAsync(string languageCode, string slug)
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        GetPublicPageBySlugQueryHandler handler = new(db, new NoOpCacheService());
        return await handler.Handle(new GetPublicPageBySlugQuery(languageCode, slug), CancellationToken.None);
    }

    [Fact]
    public async Task Published_page_in_default_language_is_found_by_bare_slug()
    {
        Result<PublicPageResponse> result = await HandleAsync("tr", "test");

        result.IsSuccess.ShouldBeTrue();
        result.Value.GjsHtml.ShouldBe("<h1>Merhaba</h1>");
        result.Value.GjsCss.ShouldBe("h1{color:red}");
        result.Value.SeoTitle.ShouldBe("Title of test");
    }

    [Fact]
    public async Task Published_page_in_non_default_language_is_found_by_prefixed_slug()
    {
        Result<PublicPageResponse> result = await HandleAsync("en", "papers");

        result.IsSuccess.ShouldBeTrue();
        result.Value.FullSlug.ShouldBe("en/papers");
    }

    [Fact]
    public async Task Default_language_page_is_not_reachable_under_a_foreign_prefix()
    {
        // /en/test would look up FullSlug "en/test" which does not exist.
        Result<PublicPageResponse> result = await HandleAsync("en", "test");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Page.NotFound");
    }

    [Fact]
    public async Task Non_default_page_is_not_reachable_without_its_prefix()
    {
        // /papers (default language) would look up FullSlug "papers" which does not exist.
        Result<PublicPageResponse> result = await HandleAsync("tr", "papers");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Page.NotFound");
    }

    [Fact]
    public async Task Draft_page_returns_a_diagnostic_not_published_error()
    {
        Result<PublicPageResponse> result = await HandleAsync("tr", "taslak-sayfa");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Page.NotPublished");
        result.Error.Description.ShouldContain("Draft");
    }

    [Fact]
    public async Task Archived_page_returns_a_diagnostic_not_published_error()
    {
        Result<PublicPageResponse> result = await HandleAsync("tr", "arsiv-sayfa");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Page.NotPublished");
    }

    [Fact]
    public async Task Soft_deleted_page_is_indistinguishable_from_not_found()
    {
        Result<PublicPageResponse> result = await HandleAsync("tr", "silinmis-sayfa");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Page.NotFound");
    }

    [Fact]
    public async Task Inactive_page_returns_a_diagnostic_inactive_error()
    {
        Result<PublicPageResponse> result = await HandleAsync("tr", "pasif-sayfa");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Page.Inactive");
    }

    [Fact]
    public async Task Unknown_slug_returns_not_found()
    {
        Result<PublicPageResponse> result = await HandleAsync("tr", "boyle-bir-sayfa-yok");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Page.NotFound");
    }

    [Fact]
    public async Task An_empty_slug_resolves_the_default_languages_homepage()
    {
        Result<PublicPageResponse> result = await HandleAsync("tr", "");

        result.IsSuccess.ShouldBeTrue();
        result.Value.FullSlug.ShouldBe("");
    }

    [Fact]
    public async Task An_empty_slug_resolves_a_non_default_languages_homepage_to_just_its_code()
    {
        Result<PublicPageResponse> result = await HandleAsync("en", "");

        result.IsSuccess.ShouldBeTrue();
        result.Value.FullSlug.ShouldBe("en");
    }

    [Fact]
    public async Task Language_code_comparison_is_case_insensitive()
    {
        Result<PublicPageResponse> result = await HandleAsync("TR", "test");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Page_with_no_page_group_has_no_alternates()
    {
        Result<PublicPageResponse> result = await HandleAsync("tr", "test");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Alternates.ShouldBeEmpty();
    }

    [Fact]
    public async Task Default_language_page_lists_its_non_default_sibling_as_an_alternate()
    {
        Result<PublicPageResponse> result = await HandleAsync("tr", "hakkimizda");

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsDefaultLanguage.ShouldBeTrue();
        result.Value.Alternates.ShouldHaveSingleItem();
        PageAlternateResponse alt = result.Value.Alternates[0];
        alt.TwoLetterCode.ShouldBe("en");
        alt.FullSlug.ShouldBe("en/about");
        alt.IsDefaultLanguage.ShouldBeFalse();
    }

    [Fact]
    public async Task Non_default_language_page_lists_the_default_sibling_as_an_alternate()
    {
        Result<PublicPageResponse> result = await HandleAsync("en", "about");

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsDefaultLanguage.ShouldBeFalse();
        result.Value.Alternates.ShouldHaveSingleItem();
        PageAlternateResponse alt = result.Value.Alternates[0];
        alt.TwoLetterCode.ShouldBe("tr");
        alt.FullSlug.ShouldBe("hakkimizda");
        alt.IsDefaultLanguage.ShouldBeTrue();
    }
}
