using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Pages.CreatePage;
using Application.Features.Commands.Pages.UpdatePage;
using Domain.Entities.Languages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// <c>PageInfo.ComputeFullSlug</c> walks <c>ParentPage.ParentPage.…</c> all the way
/// to the root — its own doc comment says so — but every caller that loads or
/// assigns a parent only loaded one level of that chain. A category three levels
/// deep (home → category → product, exactly <c>PageHierarchy.MaxDepth</c>) silently
/// lost every segment above its immediate parent: the product's stored URL came out
/// "category/product" instead of "home/category/product", with no error anywhere —
/// wrong canonical, wrong og:url, wrong sitemap entry, wrong breadcrumb, all from a
/// perfectly ordinary three-level site structure. Found while building exactly this
/// hierarchy to test the site's SEO output end to end.
/// </summary>
public sealed class NestedPageFullSlugTests
{
    private const int TurkishId = 1;

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            // ApplicationDbContext wraps SaveChanges in a transaction, which the
            // InMemory provider cannot honour.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new NestedPageTestUserContext());

    private async Task SeedLanguageAsync()
    {
        using ApplicationDbContext db = CreateDb();
        db.Languages.Add(new Language
        {
            Id = TurkishId,
            NameInNative = "Türkçe",
            NameInEnglish = "Turkish",
            TwoLetterCode = "tr",
            IsDefault = true,
            IsActive = true,
            IsPublished = true,
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> CreateAsync(string title, string slug, int? parentId)
    {
        using ApplicationDbContext db = CreateDb();
        Result<int> result = await new CreatePageCommandHandler(db).Handle(
            new CreatePageCommand(title, slug, TurkishId, parentId), CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static UpdatePageSeoMeta BlankSeo() => new(
        IsCanonical: true, CanonicalUrl: null, MetaDescription: "", MetaAuthor: "",
        NoIndex: false, NoFollow: false, FocusKeyword: null, StructuredData: null,
        OgTitle: "", OgDescription: "", OgType: "website", OgImage: null, OgUrl: null,
        TwitterCard: null, TwitterSite: null);

    private async Task<Result> SetParentAsync(int pageId, string title, string slug, int? parentId)
    {
        using ApplicationDbContext db = CreateDb();
        Result result = await new UpdatePageCommandHandler(db, new NestedPageTestUserContext()).Handle(
            new UpdatePageCommand(pageId, title, slug, Domain.Entities.PageInfos.PageStatus.Draft,
                GjsHtml: null, GjsCss: null, GjsData: null, Seo: BlankSeo(), ParentPageId: parentId),
            CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
        return result;
    }

    private async Task<string> GetFullSlugAsync(int pageId)
    {
        using ApplicationDbContext db = CreateDb();
        return (await db.PageInfos.SingleAsync(p => p.Id == pageId)).FullSlug;
    }

    [Fact]
    public async Task Creating_a_page_two_levels_under_its_parent_keeps_every_ancestor_segment()
    {
        await SeedLanguageAsync();

        int home = await CreateAsync("Ana Sayfa", "home", parentId: null);
        int category = await CreateAsync("Ürünlerimiz", "urunlerimiz", parentId: home);
        // The product is created WITH its parent already set — this is the
        // CreatePageCommandHandler path, not UpdatePageCommandHandler.
        int product = await CreateAsync("Kırmızı Gül", "kirmizi-gul", parentId: category);

        (await GetFullSlugAsync(product)).ShouldBe("urunlerimiz/kirmizi-gul");
    }

    [Fact]
    public async Task Reparenting_a_page_under_a_nested_parent_keeps_every_ancestor_segment()
    {
        await SeedLanguageAsync();

        int home = await CreateAsync("Ana Sayfa", "home", parentId: null);
        int category = await CreateAsync("Ürünlerimiz", "urunlerimiz", parentId: null);
        int product = await CreateAsync("Kırmızı Gül", "kirmizi-gul", parentId: null);

        // Reproduces the exact live sequence: attach the category to home, THEN
        // attach the product to the (now nested) category — via UpdatePageCommand,
        // not at creation time.
        (await SetParentAsync(category, "Ürünlerimiz", "urunlerimiz", home)).IsSuccess.ShouldBeTrue();
        (await SetParentAsync(product, "Kırmızı Gül", "kirmizi-gul", category)).IsSuccess.ShouldBeTrue();

        (await GetFullSlugAsync(product)).ShouldBe("urunlerimiz/kirmizi-gul");
    }

    [Fact]
    public async Task Resaving_an_already_nested_page_without_touching_its_parent_keeps_the_full_slug()
    {
        // ComputeFullSlug runs unconditionally on every save, parent changed or
        // not — a plain re-save (edit the title, hit save) must not be the moment
        // a three-level page's URL quietly loses its category segment.
        await SeedLanguageAsync();

        int home = await CreateAsync("Ana Sayfa", "home", parentId: null);
        int category = await CreateAsync("Ürünlerimiz", "urunlerimiz", parentId: home);
        int product = await CreateAsync("Kırmızı Gül", "kirmizi-gul", parentId: category);

        (await SetParentAsync(product, "Kırmızı Gül (güncellendi)", "kirmizi-gul", category)).IsSuccess.ShouldBeTrue();

        (await GetFullSlugAsync(product)).ShouldBe("urunlerimiz/kirmizi-gul");
    }

    private sealed class NestedPageTestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
