using Application.Services;
using Domain.Entities.PublicMedia;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using SharedKernel.Content;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// A rendered page carries a tag for everything it was built from, so the CMS can
/// evict exactly the pages a change touches — above all, every page using a linked
/// template when that template is edited.
/// </summary>
public sealed class PageCacheTagsTests
{
    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();

    public PageCacheTagsTests()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        db.PageTemplates.AddRange(
            new PublicPageTemplate
            {
                Id = 1, Name = "Menü", Type = PublicPageTemplateType.Menu, IsLinked = true,
                GjsHtml = """<header>MENU<div class="elevare-tpl-ref" data-elevare-template-id="2"></div></header>""",
            },
            new PublicPageTemplate { Id = 2, Name = "Arama", Type = PublicPageTemplateType.Other, IsLinked = true, GjsHtml = "<form></form>" });
        db.PageInfos.AddRange(
            Page(10, null, "makaleler", "makaleler"),
            Page(11, 10, "bit", "makaleler/bit"));
        db.MediaFiles.Add(new PublicMediaFile { Id = 1, FilePath = "/uploads/images/2026/10/a.jpg", MimeType = "image/jpeg", Width = 800, Height = 600 });
        db.SaveChanges();
    }

    private static PublicPage Page(int id, int? parentId, string slug, string fullSlug) => new()
    {
        Id = id, LanguageId = 1, Slug = slug, FullSlug = fullSlug, ParentPageId = parentId,
        SeoTitle = slug, PageStatus = PublicPageStatus.Published, IsActive = true,
    };

    [Fact]
    public async Task A_page_is_tagged_with_every_linked_template_it_resolved_nested_ones_included()
    {
        PageCacheDependencies dependencies = new();
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        Result<ResolvedTemplateContent> result = await new TemplateResolutionService(db, dependencies).ResolveAsync(
            """<div class="elevare-tpl-ref" data-elevare-template-id="1"></div>""", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        dependencies.Tags.ShouldBe([PageCacheTags.All, PageCacheTags.Template(1), PageCacheTags.Template(2)], ignoreOrder: true);
    }

    [Fact]
    public async Task A_breadcrumb_ties_the_page_to_its_ancestors()
    {
        PageCacheDependencies dependencies = new();
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        const string block =
            "<div data-elevare-breadcrumb data-elevare-breadcrumb-home=\"Home\"><ol>" +
            "<li data-elevare-crumb-template><a href=\"#\" data-elevare-crumb-link>x</a></li></ol></div>";

        await new BreadcrumbResolutionService(db, dependencies).ResolveAsync(block, 11, CancellationToken.None);

        dependencies.Tags.ShouldContain(PageCacheTags.PageInfo(10));
    }

    [Fact]
    public async Task Previous_next_links_make_the_page_a_listing()
    {
        PageCacheDependencies dependencies = new();
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        await new AdjacentPageResolutionService(db, dependencies).ResolveAsync(
            "<div data-elevare-adjacent></div>", 11, CancellationToken.None);

        dependencies.Tags.ShouldContain(PageCacheTags.Listing);
    }

    [Fact]
    public async Task An_uploaded_image_ties_the_page_to_the_media_library_and_nothing_else_does()
    {
        PageCacheDependencies withImage = new();
        PageCacheDependencies without = new();
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        await new ResponsiveImageResolutionService(db, withImage).ResolveAsync(
            """<img src="/uploads/images/2026/10/a.jpg">""", null, CancellationToken.None);
        await new ResponsiveImageResolutionService(db, without).ResolveAsync(
            """<p>/uploads/ yalnızca metin</p>""", null, CancellationToken.None);

        withImage.Tags.ShouldContain(PageCacheTags.Media);
        without.Tags.ShouldNotContain(PageCacheTags.Media);
    }
}
