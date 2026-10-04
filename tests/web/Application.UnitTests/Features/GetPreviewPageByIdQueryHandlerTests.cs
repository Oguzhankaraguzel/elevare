using Application.Features.Queries.Pages.GetPreviewPageById;
using Application.Features.Queries.Pages.GetPublicPageBySlug;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Features;

/// <summary>
/// Unlike <see cref="GetPublicPageBySlugQueryHandlerTests"/>, this handler must find
/// Draft/Archived/inactive pages just fine — access control lives entirely in the
/// signed-token check that happens before this query is ever sent. Only soft-deleted
/// pages should stay unreachable (via the entity's own query filter).
/// </summary>
public sealed class GetPreviewPageByIdQueryHandlerTests
{
    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();

    public GetPreviewPageByIdQueryHandlerTests()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        db.Languages.AddRange(
            new PublicLanguage { Id = 1, TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true },
            new PublicLanguage { Id = 2, TwoLetterCode = "en", IsDefault = false, IsActive = true, IsPublished = true });

        db.PageInfos.AddRange(
            NewPage(1, "test", PublicPageStatus.Published, languageId: 1,
                content: new PublicPageContent { Id = 1, PageInfoId = 1, GjsHtml = "<h1>Merhaba</h1>", GjsCss = "h1{color:red}" }),
            NewPage(2, "taslak-sayfa", PublicPageStatus.Draft, languageId: 1),
            NewPage(3, "arsiv-sayfa", PublicPageStatus.Archived, languageId: 1),
            NewPage(4, "pasif-sayfa", PublicPageStatus.Published, languageId: 1, isActive: false),
            NewPage(5, "silinmis-sayfa", PublicPageStatus.Published, languageId: 1, isDeleted: true),
            // Language group: 6 (tr, default) <-> 7 (en) share pageGroupId 100.
            NewPage(6, "hakkimizda", PublicPageStatus.Published, languageId: 1, pageGroupId: 100),
            NewPage(7, "en/about", PublicPageStatus.Published, languageId: 2, pageGroupId: 100));

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

    private async Task<Result<PublicPageResponse>> HandleAsync(int pageId)
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        GetPreviewPageByIdQueryHandler handler = new(db);
        return await handler.Handle(new GetPreviewPageByIdQuery(pageId), CancellationToken.None);
    }

    [Fact]
    public async Task Published_page_is_found()
    {
        Result<PublicPageResponse> result = await HandleAsync(1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.GjsHtml.ShouldBe("<h1>Merhaba</h1>");
        result.Value.LanguageCode.ShouldBe("tr");
    }

    [Fact]
    public async Task Draft_page_is_found_unlike_the_public_slug_lookup()
    {
        Result<PublicPageResponse> result = await HandleAsync(2);

        result.IsSuccess.ShouldBeTrue();
        result.Value.FullSlug.ShouldBe("taslak-sayfa");
    }

    [Fact]
    public async Task Archived_page_is_found_unlike_the_public_slug_lookup()
    {
        Result<PublicPageResponse> result = await HandleAsync(3);

        result.IsSuccess.ShouldBeTrue();
        result.Value.FullSlug.ShouldBe("arsiv-sayfa");
    }

    [Fact]
    public async Task Inactive_page_is_found_unlike_the_public_slug_lookup()
    {
        Result<PublicPageResponse> result = await HandleAsync(4);

        result.IsSuccess.ShouldBeTrue();
        result.Value.FullSlug.ShouldBe("pasif-sayfa");
    }

    [Fact]
    public async Task Soft_deleted_page_is_still_not_found()
    {
        Result<PublicPageResponse> result = await HandleAsync(5);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Page.PreviewNotFound");
    }

    [Fact]
    public async Task Unknown_id_returns_not_found()
    {
        Result<PublicPageResponse> result = await HandleAsync(999);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Page.PreviewNotFound");
    }

    [Fact]
    public async Task Default_language_page_lists_its_non_default_sibling_as_an_alternate()
    {
        Result<PublicPageResponse> result = await HandleAsync(6);

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsDefaultLanguage.ShouldBeTrue();
        result.Value.Alternates.ShouldHaveSingleItem();
        PageAlternateResponse alt = result.Value.Alternates[0];
        alt.TwoLetterCode.ShouldBe("en");
        alt.FullSlug.ShouldBe("en/about");
    }

    [Fact]
    public async Task Non_default_language_page_reports_its_own_language_code()
    {
        Result<PublicPageResponse> result = await HandleAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.LanguageCode.ShouldBe("en");
        result.Value.IsDefaultLanguage.ShouldBeFalse();
    }
}
