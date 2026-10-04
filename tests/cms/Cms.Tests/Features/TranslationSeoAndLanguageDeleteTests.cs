using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Languages.DeleteLanguage;
using Application.Features.Commands.Pages.CreatePageTranslation;
using Application.Features.Commands.Pages.DeletePage;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.SiteSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// A translation starts from the source page's texts but not from what names the
/// source page itself; and a language with pages cannot be deleted out from under
/// them.
/// </summary>
public sealed class TranslationSeoAndLanguageDeleteTests
{
    private const int TurkishId = 1;
    private const int EnglishId = 2;

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new TestUser());

    private async Task<int> SeedAsync()
    {
        using ApplicationDbContext db = CreateDb();
        var turkish = new Language { Id = TurkishId, NameInNative = "Türkçe", NameInEnglish = "Turkish", TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true };
        db.Languages.AddRange(turkish,
            new Language { Id = EnglishId, NameInNative = "English", NameInEnglish = "English", TwoLetterCode = "en", IsActive = true, IsPublished = true });
        db.SiteSettings.Add(new SiteSetting { Key = "Advanced.PublicSiteBaseUrl", DisplayName = "Site", Value = "https://www.example.com/" });
        var page = new PageInfo
        {
            Slug = "hakkimda", PageStatus = PageStatus.Published, LanguageId = TurkishId, Language = turkish, Kind = PageKind.Corporate,
            SeoMeta = new SeoMeta
            {
                Title = "Hakkımda", MetaDescription = "Ben kimim", NoIndex = true, OgImage = "/uploads/me.jpg",
                OgUrl = "https://www.example.com/hakkimda",
                StructuredData = """{"@context":"https://schema.org","@type":"WebPage","url":"https://www.example.com/hakkimda","inLanguage":"tr"}""",
            },
            Content = new PageContent { GjsHtml = "<h1>Hakkımda</h1>" },
        };
        page.ComputeFullSlug("tr");
        db.PageInfos.Add(page);
        await db.SaveChangesAsync(CancellationToken.None);
        return page.Id;
    }

    private async Task<PageInfo> TranslateAsync(int sourceId)
    {
        int id;
        using (ApplicationDbContext db = CreateDb())
            id = (await new CreatePageTranslationCommandHandler(db).Handle(new CreatePageTranslationCommand(sourceId, EnglishId), CancellationToken.None)).Value;
        using ApplicationDbContext read = CreateDb();
        return await read.PageInfos.Include(p => p.Content).SingleAsync(p => p.Id == id);
    }

    private async Task<Result> DeleteLanguageAsync(int id)
    {
        using ApplicationDbContext db = CreateDb();
        Result result = await new DeleteLanguageCommandHandler(db).Handle(new DeleteLanguageCommand(id), CancellationToken.None);
        if (result.IsSuccess) await db.SaveChangesAsync(CancellationToken.None);
        return result;
    }

    [Fact]
    public async Task A_translation_keeps_the_texts_but_gets_its_own_address_and_no_structured_data()
    {
        PageInfo en = await TranslateAsync(await SeedAsync());

        en.Content!.GjsHtml.ShouldBe("<h1>Hakkımda</h1>");
        en.SeoMeta.Title.ShouldBe("Hakkımda");
        en.SeoMeta.MetaDescription.ShouldBe("Ben kimim");
        en.SeoMeta.OgImage.ShouldBe("/uploads/me.jpg");
        en.SeoMeta.NoIndex.ShouldBeTrue();
        en.Kind.ShouldBe(PageKind.Corporate);
        // What named the Turkish page does not follow it.
        en.SeoMeta.OgUrl.ShouldBe("https://www.example.com/en/hakkimda");
        en.SeoMeta.StructuredData.ShouldBeNull();
        en.SeoMeta.CanonicalUrl.ShouldBeNull();
    }

    [Fact]
    public async Task A_language_with_pages_cannot_be_deleted()
    {
        await TranslateAsync(await SeedAsync());

        (await DeleteLanguageAsync(EnglishId)).Error.ShouldBe(LanguageErrors.HasPages);

        using ApplicationDbContext db = CreateDb();
        (await db.Languages.AnyAsync(l => l.Id == EnglishId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Once_its_pages_are_deleted_the_language_can_go()
    {
        PageInfo en = await TranslateAsync(await SeedAsync());
        using (ApplicationDbContext db = CreateDb())
        {
            (await new DeletePageCommandHandler(db).Handle(new DeletePageCommand(en.Id), CancellationToken.None)).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync(CancellationToken.None);
        }

        (await DeleteLanguageAsync(EnglishId)).IsSuccess.ShouldBeTrue();
    }

    private sealed class TestUser : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
