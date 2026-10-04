using Application.Abstraction.Services.Authentication;
using Application.Features.Queries.Pages.GetAdjacentPreview;
using Application.Features.Queries.Pages.GetBreadcrumbPreview;
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
/// The editor's canvas previews of the "Sayfa Yolu" and "Önceki / Sonraki Yazı"
/// blocks follow the public site's rules (BreadcrumbResolutionService,
/// AdjacentPageResolutionService): what the canvas shows is what the page gets.
/// </summary>
public sealed class CanvasPreviewQueryHandlerTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new TestUserContext());

    private readonly Dictionary<string, int> _ids = [];

    private async Task SeedAsync()
    {
        using ApplicationDbContext db = CreateDb();
        var turkish = new Language { Id = 1, NameInNative = "Türkçe", NameInEnglish = "Turkish", TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true };
        var english = new Language { Id = 2, NameInNative = "English", NameInEnglish = "English", TwoLetterCode = "en", IsActive = true, IsPublished = true };
        db.Languages.AddRange(turkish, english);

        async Task Add(string key, Language language, string slug, string fullSlug, string? parent, string title,
            PageStatus status = PageStatus.Published, DateTime? published = null)
        {
            var page = new PageInfo
            {
                Slug = slug, FullSlug = fullSlug, LanguageId = language.Id, ParentPageId = parent is null ? null : _ids[parent],
                PageStatus = status, IsActive = true, PublishedAt = published,
                SeoMeta = new SeoMeta { Title = title }, Content = new PageContent(),
            };
            db.PageInfos.Add(page);
            await db.SaveChangesAsync(CancellationToken.None);
            _ids[key] = page.Id;
        }

        await Add("trHome", turkish, "home", "", null, "Ana Sayfa");
        await Add("makaleler", turkish, "makaleler", "makaleler", "trHome", "Makaleler");
        await Add("taslak", turkish, "taslak", "makaleler/taslak", "makaleler", "Taslak Bölüm", PageStatus.Draft);
        await Add("derin", turkish, "derin", "makaleler/taslak/derin", "taslak", "Derin Yazı");
        await Add("a", turkish, "a", "makaleler/a", "makaleler", "A", published: Utc(2023, 1, 1));
        await Add("b", turkish, "b", "makaleler/b", "makaleler", "B", published: Utc(2023, 6, 1));
        await Add("c", turkish, "c", "makaleler/c", "makaleler", "C", published: Utc(2024, 1, 1));
        await Add("enHome", english, "home", "en", null, "Home");
        await Add("articles", english, "articles", "en/articles", "enHome", "Articles");
        await Add("post", english, "post", "en/articles/post", "articles", "A Post");
    }

    private static DateTime Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    private async Task<BreadcrumbPreviewResponse> TrailAsync(string key)
    {
        using ApplicationDbContext db = CreateDb();
        Result<BreadcrumbPreviewResponse> result = await new GetBreadcrumbPreviewQueryHandler(db)
            .Handle(new GetBreadcrumbPreviewQuery(_ids[key]), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private async Task<AdjacentPreviewResponse> AdjacentAsync(string key)
    {
        using ApplicationDbContext db = CreateDb();
        Result<AdjacentPreviewResponse> result = await new GetAdjacentPreviewQueryHandler(db)
            .Handle(new GetAdjacentPreviewQuery(_ids[key]), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    [Fact]
    public async Task An_english_trail_starts_at_the_english_homepage()
    {
        await SeedAsync();

        BreadcrumbPreviewResponse trail = await TrailAsync("post");

        trail.HomePath.ShouldBe("/en");
        trail.IsHome.ShouldBeFalse();
        trail.Crumbs.Select(c => (c.Title, c.Path)).ShouldBe([("Articles", "/en/articles"), ("A Post", "/en/articles/post")]);
    }

    [Fact]
    public async Task A_draft_ancestor_is_left_out_as_on_the_site()
    {
        await SeedAsync();

        BreadcrumbPreviewResponse trail = await TrailAsync("derin");

        trail.HomePath.ShouldBe("/");
        trail.Crumbs.Select(c => c.Title).ShouldBe(["Makaleler", "Derin Yazı"]);
    }

    [Fact]
    public async Task The_homepage_has_no_home_crumb_above_it()
    {
        await SeedAsync();

        BreadcrumbPreviewResponse trail = await TrailAsync("trHome");

        trail.IsHome.ShouldBeTrue();
        trail.Crumbs.Select(c => c.Title).ShouldBe(["Ana Sayfa"]);
    }

    [Fact]
    public async Task Previous_and_next_are_the_live_siblings_by_publish_date()
    {
        await SeedAsync();

        AdjacentPreviewResponse middle = await AdjacentAsync("b");
        AdjacentPreviewResponse first = await AdjacentAsync("a");

        middle.Previous!.Title.ShouldBe("A");
        middle.Next!.Path.ShouldBe("/makaleler/c");
        first.Previous.ShouldBeNull();
    }

    [Fact]
    public async Task A_top_level_page_has_no_neighbours()
    {
        await SeedAsync();

        AdjacentPreviewResponse home = await AdjacentAsync("trHome");

        home.Previous.ShouldBeNull();
        home.Next.ShouldBeNull();
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
