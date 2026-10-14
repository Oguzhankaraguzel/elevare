using Application.Abstraction.Behaviors;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.PageTemplates;
using Domain.Entities.SiteSettings;
using Domain.Entities.UserNotes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Content;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// A save tells the public site which cached pages it made stale — read from what
/// was actually written, so a note clears nothing, a page clears what shows it, a
/// linked template clears every page using it, and anything not understood clears
/// the whole site, as every save did before.
/// </summary>
public sealed class PublicSiteChangeTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    public PublicSiteChangeTests()
    {
        using ApplicationDbContext seed = CreateDb();
        seed.Languages.Add(new Language { Id = 1, NameInNative = "Türkçe", NameInEnglish = "Turkish", TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true });
        seed.PageGroups.Add(new PageGroup { Id = 7, Name = "hakkinda" });
        seed.PageInfos.Add(new PageInfo
        {
            Id = 3, Slug = "hakkinda", FullSlug = "hakkinda", LanguageId = 1, PageGroupId = 7, PageStatus = PageStatus.Published,
            SeoMeta = new SeoMeta { Title = "Hakkında" }, Content = new PageContent { GjsHtml = "<p>Eski</p>" },
        });
        seed.PageTemplates.Add(new PageTemplate { Id = 5, Name = "Menü", IsLinked = true, GjsHtml = "<nav>Eski</nav>" });
        seed.SaveChanges();
    }

    private ApplicationDbContext CreateDb() => new(_options, new User());

    private static async Task<PublicSiteChange> SaveAsync(ApplicationDbContext db)
    {
        await db.SaveChangesAsync(CancellationToken.None);
        return db.TakePublicSiteChange();
    }

    [Fact]
    public async Task A_note_touches_nothing_on_the_site()
    {
        using ApplicationDbContext db = CreateDb();
        db.UserNotes.Add(new UserNote { Title = "Yapılacak", Content = "Menüyü düzelt" });

        PublicSiteChange change = await SaveAsync(db);

        change.Saves.ShouldBe(1);
        change.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task Editing_a_linked_template_evicts_the_pages_using_it_and_only_those()
    {
        using ApplicationDbContext db = CreateDb();
        PageTemplate template = await db.PageTemplates.SingleAsync(t => t.Id == 5);
        template.GjsHtml = "<nav>Yeni</nav>";

        PublicSiteChange change = await SaveAsync(db);

        change.IsEverything.ShouldBeFalse();
        change.Tags.ShouldBe([PageCacheTags.Template(5)]);
    }

    [Fact]
    public async Task An_unpublished_preview_copy_touches_nothing()
    {
        using ApplicationDbContext db = CreateDb();
        PageTemplate template = await db.PageTemplates.SingleAsync(t => t.Id == 5);
        template.PreviewGjsHtml = "<nav>Taslak</nav>";
        PageContent content = await db.PageContents.SingleAsync(c => c.PageInfoId == 3);
        content.PreviewGjsHtml = "<p>Taslak</p>";

        PublicSiteChange change = await SaveAsync(db);

        change.Saves.ShouldBe(1);
        change.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task A_pages_content_evicts_the_page_and_the_lists_showing_it()
    {
        using ApplicationDbContext db = CreateDb();
        PageContent content = await db.PageContents.SingleAsync(c => c.PageInfoId == 3);
        content.GjsHtml = "<p>Yeni</p>";

        PublicSiteChange change = await SaveAsync(db);

        change.Tags.ShouldBe([PageCacheTags.Page(3), PageCacheTags.Listing], ignoreOrder: true);
    }

    [Fact]
    public async Task A_pages_title_also_reaches_breadcrumbs_translations_and_a_cached_404()
    {
        using ApplicationDbContext db = CreateDb();
        PageInfo page = await db.PageInfos.SingleAsync(p => p.Id == 3);
        page.SeoMeta.Title = "Hakkımda";
        page.Slug = "hakkimda";

        PublicSiteChange change = await SaveAsync(db);

        change.Tags.ShouldBe(
            [PageCacheTags.Page(3), PageCacheTags.PageInfo(3), PageCacheTags.Group(7), PageCacheTags.Listing, PageCacheTags.NotFound],
            ignoreOrder: true);
    }

    [Fact]
    public async Task A_site_setting_clears_everything()
    {
        using ApplicationDbContext db = CreateDb();
        db.SiteSettings.Add(new SiteSetting { Key = "Site.Name", DisplayName = "Site", Value = "Elevare" });
        db.UserNotes.Add(new UserNote { Title = "Not", Content = "x" });

        PublicSiteChange change = await SaveAsync(db);

        change.IsEverything.ShouldBeTrue();
    }

    [Fact]
    public async Task Changes_are_taken_once()
    {
        using ApplicationDbContext db = CreateDb();
        (await db.PageTemplates.SingleAsync(t => t.Id == 5)).GjsHtml = "<nav>Yeni</nav>";
        await SaveAsync(db);

        db.TakePublicSiteChange().Saves.ShouldBe(0);
    }

    [Fact]
    public async Task A_command_that_succeeded_without_a_visible_save_clears_everything()
    {
        using ApplicationDbContext db = CreateDb();
        RecordingInvalidator invalidator = new();
        var behavior = new PublicSiteCacheInvalidationPipelineBehavior<NoOpCommand, Result>(invalidator, db);

        await behavior.Handle(new NoOpCommand(), () => Task.FromResult(Result.Success()), CancellationToken.None);

        invalidator.Requests.ShouldHaveSingleItem().IsEverything.ShouldBeTrue();
    }

    [Fact]
    public async Task A_command_that_saved_only_private_rows_asks_for_nothing()
    {
        using ApplicationDbContext db = CreateDb();
        RecordingInvalidator invalidator = new();
        var behavior = new PublicSiteCacheInvalidationPipelineBehavior<NoOpCommand, Result>(invalidator, db);

        await behavior.Handle(new NoOpCommand(), async () =>
        {
            db.UserNotes.Add(new UserNote { Title = "Not", Content = "x" });
            await db.SaveChangesAsync(CancellationToken.None);
            return Result.Success();
        }, CancellationToken.None);

        invalidator.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Requests_sent_together_add_up_and_everything_wins()
    {
        var pending = PublicSiteChange.ForTags(PageCacheTags.Template(5));
        pending.Merge(PublicSiteChange.ForTags(PageCacheTags.Page(3)));
        pending.Tags.ShouldBe([PageCacheTags.Template(5), PageCacheTags.Page(3)], ignoreOrder: true);

        pending.Merge(PublicSiteChange.Everything());
        pending.Merge(PublicSiteChange.ForTags(PageCacheTags.Listing));
        pending.IsEverything.ShouldBeTrue();
        pending.Tags.ShouldBeEmpty();
    }

    public sealed record NoOpCommand : ICommand;

    private sealed class RecordingInvalidator : IPublicSiteCacheInvalidator
    {
        public List<PublicSiteChange> Requests { get; } = [];

        public void RequestInvalidation(PublicSiteChange change) => Requests.Add(change);
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
