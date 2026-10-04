using Application.Abstraction.Services.Authentication;
using Application.Features.Queries.Pages.GetPageDirectory;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// Covers <see cref="GetPageDirectoryQueryHandler"/>, the page builder's page
/// pickers: every page at every depth — the parent picker's candidates stopped above
/// the deepest level, so an article three levels down could not be linked — and, for
/// a template shared by every language, every language's pages.
/// </summary>
public sealed class GetPageDirectoryQueryHandlerTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new TestUserContext());

    private async Task SeedAsync()
    {
        using ApplicationDbContext db = CreateDb();
        var turkish = new Language { Id = 1, NameInNative = "Türkçe", NameInEnglish = "Turkish", TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true };
        var english = new Language { Id = 2, NameInNative = "English", NameInEnglish = "English", TwoLetterCode = "en", IsActive = true, IsPublished = true };
        db.Languages.AddRange(turkish, english);

        static PageInfo Page(Language language, string fullSlug, string title) => new()
        {
            Slug = fullSlug.Split('/')[^1], FullSlug = fullSlug, PageStatus = PageStatus.Published,
            LanguageId = language.Id, Language = language,
            SeoMeta = new SeoMeta { Title = title }, Content = new PageContent(),
        };

        PageInfo home = Page(turkish, "", "Ana Sayfa");
        db.PageInfos.Add(home);
        await db.SaveChangesAsync(CancellationToken.None);
        PageInfo articles = Page(turkish, "makaleler", "Makaleler");
        articles.ParentPageId = home.Id;
        db.PageInfos.Add(articles);
        await db.SaveChangesAsync(CancellationToken.None);
        PageInfo article = Page(turkish, "makaleler/bit", "C# ile Bit Tabanlı İşlemler");
        article.ParentPageId = articles.Id;
        db.PageInfos.AddRange(article, Page(english, "en", "Home"));
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<List<PageDirectoryEntryResponse>> DirectoryAsync(int? languageId)
    {
        using ApplicationDbContext db = CreateDb();
        Result<List<PageDirectoryEntryResponse>> result = await new GetPageDirectoryQueryHandler(db).Handle(
            new GetPageDirectoryQuery(languageId), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    [Fact]
    public async Task Every_page_of_the_language_is_listed_at_every_depth()
    {
        await SeedAsync();

        List<PageDirectoryEntryResponse> pages = await DirectoryAsync(1);

        pages.Select(p => p.FullSlug).ShouldBe(["", "makaleler", "makaleler/bit"]);
        pages[^1].Title.ShouldBe("C# ile Bit Tabanlı İşlemler");
    }

    [Fact]
    public async Task No_language_lists_every_languages_pages()
    {
        await SeedAsync();

        List<PageDirectoryEntryResponse> pages = await DirectoryAsync(null);

        pages.Select(p => p.FullSlug).ShouldBe(["", "en", "makaleler", "makaleler/bit"]);
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
