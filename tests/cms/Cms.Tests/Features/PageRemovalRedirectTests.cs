using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Pages;
using Application.Features.Commands.Pages.UpdatePageStatus;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.Redirects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// A page keeps one redirect per address it has had. Taking it off the site used to
/// grab whichever of those rules came first and move it onto the current address —
/// so the address that rule had been keeping alive started answering 404.
/// </summary>
public sealed class PageRemovalRedirectTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new User());

    private async Task SeedMovedPageAsync()
    {
        using ApplicationDbContext db = CreateDb();
        db.Languages.Add(new Language { Id = 1, NameInNative = "Türkçe", NameInEnglish = "Turkish", TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true });
        db.PageInfos.Add(new PageInfo { Id = 7, Slug = "c", FullSlug = "c", LanguageId = 1, PageStatus = PageStatus.Published,
            SeoMeta = new SeoMeta { Title = "C" }, Content = new PageContent() });
        await db.SaveChangesAsync();

        // The page lived at "a", then "b", and is at "c" now.
        await RedirectResolution.UpsertForSlugChangeAsync(db, 7, "a", "b", CancellationToken.None);
        await db.SaveChangesAsync();
        await RedirectResolution.UpsertForSlugChangeAsync(db, 7, "b", "c", CancellationToken.None);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Every_earlier_address_names_the_pages_current_one()
    {
        await SeedMovedPageAsync();

        using ApplicationDbContext read = CreateDb();
        List<Redirect> rules = await read.Redirects.OrderBy(r => r.OldPath).ToListAsync();
        rules.Select(r => (r.OldPath, r.NewPath, r.SourcePageId)).ShouldBe([("a", "c", 7), ("b", "c", 7)]);
    }

    [Fact]
    public async Task Archiving_the_page_keeps_its_earlier_addresses_and_redirects_the_current_one()
    {
        await SeedMovedPageAsync();

        using (ApplicationDbContext db = CreateDb())
        {
            (await new UpdatePageStatusCommandHandler(db).Handle(
                new UpdatePageStatusCommand(7, PageStatus.Archived, "/yeni-yer"), CancellationToken.None)).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync();
        }

        using ApplicationDbContext read = CreateDb();
        List<Redirect> rules = await read.Redirects.OrderBy(r => r.OldPath).ToListAsync();
        // "a" and "b" still lead to "c", and "c" now leads where the editor said.
        rules.Select(r => (r.OldPath, r.NewPath)).ShouldBe([("a", "c"), ("b", "c"), ("c", "/yeni-yer")]);
        rules.Single(r => r.OldPath == "c").SourcePageId.ShouldBeNull();
    }

    private sealed class User : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
