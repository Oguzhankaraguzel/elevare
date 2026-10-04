using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.PageTemplates.UpdatePageTemplate;
using Application.Features.Commands.Pages;
using Application.Features.Commands.Pages.SavePreviewContent;
using Application.Features.Commands.Pages.UpdatePage;
using Application.Features.Queries.PageTemplates.GetPageTemplateById;
using Application.Features.Queries.Pages.GetPageById;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.PageTemplates;
using Domain.Entities.SiteSettings;
using Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using SharedKernel.Social;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// A save from an editor opened before someone else changed the page used to win
/// silently: another author's work, a status change from the page list, or the
/// links a page move had just rewritten were put back the way the editor last saw
/// them. The editor now sends back what it loaded, and the save is refused when
/// the page has moved on — unless the author chooses to overwrite.
/// </summary>
public sealed class PageEditConflictTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new User());

    private async Task SeedAsync()
    {
        using ApplicationDbContext db = CreateDb();
        // The editor query loads the page's author, a required relation.
        db.Users.Add(new AppUser { Id = User.Id, UserName = "editor" });
        db.Languages.Add(new Language { Id = 1, NameInNative = "Türkçe", NameInEnglish = "Turkish", TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true });
        db.SiteSettings.Add(new SiteSetting { Key = "Advanced.PublicSiteBaseUrl", DisplayName = "Site", Value = "https://www.example.com" });
        db.PageInfos.AddRange(
            new PageInfo { Id = 1, Slug = "home", FullSlug = "", LanguageId = 1, PageStatus = PageStatus.Published,
                SeoMeta = new SeoMeta { Title = "Ana Sayfa" }, Content = new PageContent { GjsHtml = "<a href=\"/urunler\">Ürünler</a>" } },
            new PageInfo { Id = 2, Slug = "urunler", FullSlug = "urunler", LanguageId = 1, ParentPageId = 1, PageStatus = PageStatus.Published,
                SeoMeta = new SeoMeta { Title = "Ürünler", OgUrl = "https://www.example.com/urunler" }, Content = new PageContent { GjsHtml = "<h1>Ürünler</h1>" } });
        await db.SaveChangesAsync();
    }

    private async Task<PageEditorResponse> OpenAsync(int id)
    {
        using ApplicationDbContext db = CreateDb();
        Result<PageEditorResponse> result = await new GetPageByIdQueryHandler(db).Handle(new GetPageByIdQuery(id), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private async Task<Result> SaveAsync(PageEditorResponse opened, string html, string? slug = null, bool overwrite = false)
    {
        using ApplicationDbContext db = CreateDb();
        UpdatePageSeoMeta seo = new(true, null, "", "", false, false, null, opened.Seo.StructuredData,
            "", "", "website", null, opened.Seo.OgUrl, null, null);
        Result result = await new UpdatePageCommandHandler(db, new User()).Handle(
            new UpdatePageCommand(opened.Id, opened.Title, slug ?? opened.Slug, opened.Status, html, "", "{}", seo,
                opened.ParentPageId, ExpectedFingerprint: opened.Fingerprint, Overwrite: overwrite),
            CancellationToken.None);
        if (result.IsSuccess)
            await db.SaveChangesAsync();
        return result;
    }

    [Fact]
    public async Task A_save_from_an_editor_opened_before_another_save_is_refused()
    {
        await SeedAsync();
        PageEditorResponse first = await OpenAsync(2);
        PageEditorResponse second = await OpenAsync(2);

        (await SaveAsync(second, "<h1>İkinci yazar</h1>")).IsSuccess.ShouldBeTrue();
        Result stale = await SaveAsync(first, "<h1>Birinci yazar</h1>");

        stale.Error.ShouldBe(PageInfoErrors.EditedElsewhere);
        (await OpenAsync(2)).GjsHtml.ShouldBe("<h1>İkinci yazar</h1>");
    }

    [Fact]
    public async Task The_author_can_choose_to_overwrite()
    {
        await SeedAsync();
        PageEditorResponse first = await OpenAsync(2);
        (await SaveAsync(await OpenAsync(2), "<h1>İkinci yazar</h1>")).IsSuccess.ShouldBeTrue();

        (await SaveAsync(first, "<h1>Birinci yazar</h1>", overwrite: true)).IsSuccess.ShouldBeTrue();

        (await OpenAsync(2)).GjsHtml.ShouldBe("<h1>Birinci yazar</h1>");
    }

    [Fact]
    public async Task Saving_again_after_reloading_what_was_saved_is_not_a_conflict()
    {
        await SeedAsync();
        (await SaveAsync(await OpenAsync(2), "<h1>Bir</h1>")).IsSuccess.ShouldBeTrue();

        (await SaveAsync(await OpenAsync(2), "<h1>İki</h1>")).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Renaming_a_page_moves_the_links_to_it_and_its_own_share_address()
    {
        await SeedAsync();
        PageEditorResponse homeEditor = await OpenAsync(1);

        (await SaveAsync(await OpenAsync(2), "<h1>Ürünler</h1>", slug: "urunlerimiz")).IsSuccess.ShouldBeTrue();

        (await OpenAsync(1)).GjsHtml.ShouldBe("<a href=\"/urunlerimiz\">Ürünler</a>");
        (await OpenAsync(2)).Seo.OgUrl.ShouldBe("https://www.example.com/urunlerimiz");
        // An editor left open on the homepage would put the old link back — refused.
        (await SaveAsync(homeEditor, "<a href=\"/urunler\">Ürünler</a>")).Error.ShouldBe(PageInfoErrors.EditedElsewhere);
    }

    [Fact]
    public async Task A_template_editor_opened_before_a_move_cannot_put_the_old_link_back()
    {
        await SeedAsync();
        using (ApplicationDbContext seed = CreateDb())
        {
            seed.PageTemplates.Add(new PageTemplate { Id = 1, Name = "Menü", IsLinked = true, GjsHtml = "<a href=\"/urunler\">Ürünler</a>" });
            await seed.SaveChangesAsync();
        }
        PageTemplateEditorResponse menuEditor;
        using (ApplicationDbContext db = CreateDb())
            menuEditor = (await new GetPageTemplateByIdQueryHandler(db).Handle(new GetPageTemplateByIdQuery(1), CancellationToken.None)).Value;

        (await SaveAsync(await OpenAsync(2), "<h1>Ürünler</h1>", slug: "urunlerimiz")).IsSuccess.ShouldBeTrue();

        using ApplicationDbContext save = CreateDb();
        Result stale = await new UpdatePageTemplateCommandHandler(save, new User()).Handle(
            new UpdatePageTemplateCommand(1, "Menü", PageTemplateType.Other, true, "<a href=\"/urunler\">Ürünler</a>", "", "{}",
                ExpectedFingerprint: menuEditor.Fingerprint),
            CancellationToken.None);
        stale.Error.ShouldBe(PageTemplateErrors.EditedElsewhere);
        (await save.PageTemplates.SingleAsync()).GjsHtml.ShouldBe("<a href=\"/urunlerimiz\">Ürünler</a>");
    }

    [Fact]
    public async Task Previewing_from_the_editor_is_not_someone_elses_change()
    {
        await SeedAsync();
        PageEditorResponse opened = await OpenAsync(2);
        using (ApplicationDbContext db = CreateDb())
        {
            (await new SavePreviewContentCommandHandler(db, new User()).Handle(
                new SavePreviewContentCommand(2, "<h1>Önizleme</h1>", ""), CancellationToken.None)).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync();
        }

        (await SaveAsync(opened, "<h1>Kaydedildi</h1>")).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Share_fields_read_back_from_jsonb_are_the_same_page()
    {
        // PostgreSQL's jsonb returns the JSON with its keys reordered and spaces
        // added. The editor kept the text it saved, the server read the column back,
        // and every second save of a page with share fields was refused as a conflict.
        var social = new SocialMeta { ImageAlt = "alt", ImageWidth = 720 };
        static PageInfo Page(string? socialJson) => new()
        {
            Slug = "urunler", SeoMeta = new SeoMeta { Title = "Ürünler", SocialJson = socialJson },
            Content = new PageContent { GjsHtml = "<h1>Ürünler</h1>" },
        };
        string asSaved = social.ToJson()!;
        string asReadBack = """{"bookTags": [], "imageAlt": "alt", "videoTags": [], "imageWidth": 720, "musicSongs": [], "articleTags": [], "bookAuthors": [], "videoActors": [], "videoWriters": [], "articleAuthors": [], "musicMusicians": [], "videoDirectors": []}""";

        PageFingerprint.Of(Page(asReadBack)).ShouldBe(PageFingerprint.Of(Page(asSaved)));
        PageFingerprint.Of(Page(asReadBack)).ShouldNotBe(PageFingerprint.Of(Page(new SocialMeta { ImageAlt = "başka" }.ToJson())));
    }

    private sealed class User : IUserContext
    {
        public static readonly Guid Id = Guid.Parse("7d4b0c2e-1f3a-4e5b-9c6d-8a7b6c5d4e3f");
        public Guid UserId => Id;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
