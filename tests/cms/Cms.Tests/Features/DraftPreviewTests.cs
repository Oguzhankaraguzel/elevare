using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Pages.SavePreviewContent;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// The editor's previews (the Preview button, the SEO analyzer) used to write the
/// same columns an approval workflow stages into. Previewing a page then replaced
/// the content waiting for approval with the unsaved canvas — the reviewer would
/// have approved something nobody submitted — and every page the analyzer read
/// was listed as "published, but its update has not gone live yet".
/// </summary>
public sealed class DraftPreviewTests
{
    private static readonly Guid EditorId = Guid.Parse("00000000-0000-0000-0000-0000000000e1");

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new DraftTestUserContext());

    private async Task<int> SeedPageAsync(string? staged)
    {
        using ApplicationDbContext db = CreateDb();
        var page = new PageInfo
        {
            Slug = "sayfa",
            PageStatus = PageStatus.Published,
            SeoMeta = new SeoMeta { Title = "Sayfa" },
            Content = new PageContent
            {
                GjsHtml = "<p>Canlı</p>",
                GjsCss = "p{}",
                PreviewGjsHtml = staged,
                PreviewGjsCss = staged is null ? null : "p{color:red}",
            },
        };
        db.PageInfos.Add(page);
        await db.SaveChangesAsync();
        return page.Id;
    }

    private async Task PreviewAsync(int pageId, string html)
    {
        using ApplicationDbContext db = CreateDb();
        Result result = await new SavePreviewContentCommandHandler(db, new DraftTestUserContext())
            .Handle(new SavePreviewContentCommand(pageId, html, "p{color:blue}"), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Previewing_leaves_content_awaiting_approval_as_it_was_submitted()
    {
        int pageId = await SeedPageAsync(staged: "<p>Onay bekliyor</p>");

        await PreviewAsync(pageId, "<p>Kaydedilmedi</p>");

        using ApplicationDbContext db = CreateDb();
        PageContent content = await db.PageContents.SingleAsync(c => c.PageInfoId == pageId);
        content.PreviewGjsHtml.ShouldBe("<p>Onay bekliyor</p>");
        content.PreviewGjsCss.ShouldBe("p{color:red}");
        content.DraftGjsHtml.ShouldBe("<p>Kaydedilmedi</p>");
        content.DraftGjsCss.ShouldBe("p{color:blue}");
        content.GjsHtml.ShouldBe("<p>Canlı</p>");
    }

    private sealed class DraftTestUserContext : IUserContext
    {
        public Guid UserId => EditorId;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
