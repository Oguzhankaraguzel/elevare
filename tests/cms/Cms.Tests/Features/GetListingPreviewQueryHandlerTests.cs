using Application.Abstraction.Services.Authentication;
using Application.Features.Queries.Pages.GetListingPreview;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.Tags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// Covers <see cref="GetListingPreviewQueryHandler"/>: what the editor says a "Sayfa
/// Listesi" block will list — the same pages, order and card text as the public
/// site — and which pages it leaves out, as found on the live site (an article sat
/// unlisted as a draft and nothing said so).
/// </summary>
public sealed class GetListingPreviewQueryHandlerTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private int _listingId;

    private ApplicationDbContext CreateDb() => new(_options, new TestUserContext());

    private async Task SeedAsync()
    {
        using ApplicationDbContext db = CreateDb();
        var turkish = new Language
        {
            Id = 1, NameInNative = "Türkçe", NameInEnglish = "Turkish", TwoLetterCode = "tr",
            IsDefault = true, IsActive = true, IsPublished = true,
        };
        db.Languages.Add(turkish);
        var csharp = new Tag { Name = "csharp", Slug = "csharp" };

        PageInfo Page(string slug, string title, PageStatus status, DateTime? publishedAt, string html = "", string description = "") => new()
        {
            Slug = slug, FullSlug = "makaleler/" + slug, PageStatus = status, LanguageId = 1, Language = turkish,
            PublishedAt = publishedAt, SeoMeta = new SeoMeta { Title = title, MetaDescription = description },
            Content = new PageContent { GjsHtml = html },
        };

        var listing = new PageInfo
        {
            Slug = "makaleler", FullSlug = "makaleler", PageStatus = PageStatus.Published, LanguageId = 1, Language = turkish,
            Kind = PageKind.Category, SeoMeta = new SeoMeta { Title = "Makaleler" }, Content = new PageContent(),
        };
        db.PageInfos.Add(listing);
        await db.SaveChangesAsync(CancellationToken.None);
        _listingId = listing.Id;

        PageInfo bit = Page("bit", "C# ile Bit Tabanlı İşlemler", PageStatus.Published, Utc(2023, 8, 13), description: "AND, OR ve XOR.");
        bit.Tags = [csharp];
        PageInfo plain = Page("aciklamasiz", "Açıklamasız", PageStatus.Published, Utc(2024, 5, 2),
            "<p>Kendi açıklaması olmayan bir yazının ilk paragrafı buradan okunur.</p><img src=\"/uploads/kapak.jpg\">");
        PageInfo draft = Page("aristo", "Nesne Tabanlı Programlama ve Aristoteles", PageStatus.Draft, null);
        PageInfo pending = Page("bekleyen", "Onay Bekleyen", PageStatus.Draft, null);
        pending.PendingStatus = PageStatus.Published;
        PageInfo archived = Page("eski", "Arşivdeki", PageStatus.Archived, Utc(2020, 1, 1));
        foreach (PageInfo child in new[] { bit, plain, draft, pending, archived })
        {
            child.ParentPageId = _listingId;
            db.PageInfos.Add(child);
        }
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static DateTime Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    private async Task<ListingPreviewResponse> PreviewAsync(string source = "self", string? tag = null, int? pageId = -1)
    {
        using ApplicationDbContext db = CreateDb();
        Result<ListingPreviewResponse> result = await new GetListingPreviewQueryHandler(db).Handle(
            new GetListingPreviewQuery(pageId == -1 ? _listingId : pageId, source, null, "newest", tag, 10), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    [Fact]
    public async Task Live_pages_are_listed_newest_first_and_the_rest_are_named_with_the_reason()
    {
        await SeedAsync();

        ListingPreviewResponse preview = await PreviewAsync();

        preview.PublishedCount.ShouldBe(2);
        preview.Items.Select(i => i.Title).ShouldBe(["Açıklamasız", "C# ile Bit Tabanlı İşlemler"]);
        preview.Hidden.Select(h => (h.Title, h.Reason)).ShouldBe(
            [("Nesne Tabanlı Programlama ve Aristoteles", "draft"), ("Onay Bekleyen", "pending"), ("Arşivdeki", "archived")],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Cards_read_as_the_public_site_fills_them()
    {
        await SeedAsync();

        ListingPreviewResponse preview = await PreviewAsync();
        ListingPreviewItem plain = preview.Items[0];
        ListingPreviewItem bit = preview.Items[1];

        plain.Summary.ShouldBe("Kendi açıklaması olmayan bir yazının ilk paragrafı buradan okunur.");
        plain.Image.ShouldBe("/uploads/kapak.jpg");
        plain.Tags.ShouldBeNull();
        bit.DateText.ShouldBe("13 Ağustos 2023");
        bit.Path.ShouldBe("/makaleler/bit");
        bit.Tags.ShouldBe("csharp");
    }

    [Fact]
    public async Task A_default_tag_narrows_the_list()
    {
        await SeedAsync();

        (await PreviewAsync(tag: "csharp")).Items.Select(i => i.Title).ShouldBe(["C# ile Bit Tabanlı İşlemler"]);
    }

    [Fact]
    public async Task A_template_has_nothing_to_preview_for_this_pages_children()
    {
        await SeedAsync();

        ListingPreviewResponse preview = await PreviewAsync(pageId: null);

        preview.PublishedCount.ShouldBe(0);
        preview.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_published_page_leaves_out_the_listing_itself()
    {
        await SeedAsync();

        (await PreviewAsync(source: "all")).Items.Select(i => i.Title).ShouldNotContain("Makaleler");
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
