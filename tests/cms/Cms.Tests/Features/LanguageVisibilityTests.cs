using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Languages;
using Application.Features.Commands.Languages.UpdateLanguage;
using Application.Features.Commands.Pages.DeletePage;
using Application.Features.Commands.Pages.UpdatePageStatus;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.Redirects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// Taking a language — or a language's homepage — off the site. Both used to be
/// either silently half-done (an unpublished language kept every page live) or
/// refused outright (a homepage could never leave Published). Now each is allowed,
/// but only once confirmed, and an unpublished language can leave temporary
/// redirects behind that are offered for removal when it comes back.
/// </summary>
public sealed class LanguageVisibilityTests
{
    private const int TurkishId = 1;
    private const int EnglishId = 2;

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private readonly SitemapRequests _sitemaps = new();

    private ApplicationDbContext CreateDb() => new(_options, new TestUserContext());

    private async Task SeedAsync()
    {
        using ApplicationDbContext db = CreateDb();
        db.Languages.AddRange(
            new Language { Id = TurkishId, NameInNative = "Türkçe", NameInEnglish = "Turkish", TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true },
            new Language { Id = EnglishId, NameInNative = "English", NameInEnglish = "English", TwoLetterCode = "en", IsActive = true, IsPublished = true });

        db.PageInfos.AddRange(
            Page(1, "home", "", TurkishId, group: 10),
            Page(2, "hakkimda", "hakkimda", TurkishId, group: 20, parent: 1),
            Page(3, "home", "en", EnglishId, group: 10),
            Page(4, "about", "en/about", EnglishId, group: 20, parent: 3),
            Page(5, "only-english", "en/only-english", EnglishId, parent: 3),
            Page(6, "draft", "en/draft", EnglishId, parent: 3, status: PageStatus.Draft),
            Page(7, "404", "en/404", EnglishId));
        await db.SaveChangesAsync();
    }

    private static PageInfo Page(int id, string slug, string fullSlug, int languageId,
        int? group = null, int? parent = null, PageStatus status = PageStatus.Published) => new()
    {
        Id = id, Slug = slug, FullSlug = fullSlug, LanguageId = languageId,
        PageGroupId = group, ParentPageId = parent, PageStatus = status,
        SeoMeta = new SeoMeta { Title = slug },
        Content = new PageContent { GjsHtml = $"<h1>{slug}</h1>" },
    };

    private async Task<Result<LanguageSaveResult>> UpdateEnglishAsync(
        bool isPublished, bool confirm = false,
        LanguageHiddenHandling whenHidden = LanguageHiddenHandling.NotFound, bool removeRedirects = false)
    {
        using ApplicationDbContext db = CreateDb();
        Result<LanguageSaveResult> result = await new UpdateLanguageCommandHandler(db, _sitemaps).Handle(
            new UpdateLanguageCommand(EnglishId, "English", "English", "en", null,
                IsDefault: false, IsRtl: false, IsPublished: isPublished, DisplayOrder: 0, IsActive: true,
                ConfirmHiding: confirm, WhenHidden: whenHidden, RemoveHiddenRedirects: removeRedirects),
            CancellationToken.None);
        if (result.IsSuccess)
            await db.SaveChangesAsync(CancellationToken.None);
        return result;
    }

    private async Task<List<Redirect>> RedirectsAsync()
    {
        using ApplicationDbContext db = CreateDb();
        return await db.Redirects.ToListAsync();
    }

    [Fact]
    public async Task Taking_a_language_off_the_site_without_confirmation_is_refused()
    {
        await SeedAsync();

        Result<LanguageSaveResult> result = await UpdateEnglishAsync(isPublished: false);

        result.Error.ShouldBe(LanguageErrors.HidingNeedsConfirmation);
        using ApplicationDbContext db = CreateDb();
        (await db.Languages.SingleAsync(l => l.Id == EnglishId)).IsPublished.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task The_default_language_can_never_be_taken_off_the_site(bool isPublished, bool isActive)
    {
        await SeedAsync();

        using ApplicationDbContext db = CreateDb();
        Result<LanguageSaveResult> result = await new UpdateLanguageCommandHandler(db, _sitemaps).Handle(
            new UpdateLanguageCommand(TurkishId, "Türkçe", "Turkish", "tr", null,
                IsDefault: true, IsRtl: false, IsPublished: isPublished, DisplayOrder: 0, IsActive: isActive,
                ConfirmHiding: true),
            CancellationToken.None);

        result.Error.ShouldBe(LanguageErrors.DefaultMustStayVisible);
    }

    [Fact]
    public async Task Redirecting_writes_a_temporary_rule_for_every_published_page_of_the_language()
    {
        await SeedAsync();

        Result<LanguageSaveResult> result = await UpdateEnglishAsync(
            isPublished: false, confirm: true, whenHidden: LanguageHiddenHandling.RedirectToDefaultLanguage);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RedirectsCreated.ShouldBe(3);

        List<Redirect> rules = await RedirectsAsync();
        rules.ShouldAllBe(r => r.IsTemporary && r.Reason == RedirectReason.LanguageUnpublished);

        // Counterparts are followed by id, with the address frozen as the fallback.
        rules.Single(r => r.OldPath == "en").SourcePageId.ShouldBe(1);
        rules.Single(r => r.OldPath == "en/about").SourcePageId.ShouldBe(2);
        rules.Single(r => r.OldPath == "en/about").NewPath.ShouldBe("/hakkimda");
        // No Turkish version: the visitor goes to the homepage instead.
        rules.Single(r => r.OldPath == "en/only-english").SourcePageId.ShouldBeNull();
        rules.Single(r => r.OldPath == "en/only-english").NewPath.ShouldBe("/");
        // A draft was never reachable, and an error page is nobody's destination.
        rules.ShouldNotContain(r => r.OldPath == "en/draft");
        rules.ShouldNotContain(r => r.OldPath == "en/404");

        _sitemaps.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Choosing_404_writes_no_redirects()
    {
        await SeedAsync();

        Result<LanguageSaveResult> result = await UpdateEnglishAsync(isPublished: false, confirm: true);

        result.IsSuccess.ShouldBeTrue();
        (await RedirectsAsync()).ShouldBeEmpty();
        _sitemaps.Count.ShouldBe(1);
    }

    [Fact]
    public async Task An_address_that_already_has_a_rule_keeps_it()
    {
        await SeedAsync();
        using (ApplicationDbContext db = CreateDb())
        {
            db.Redirects.Add(new Redirect { OldPath = "en/about", NewPath = "/elle-secilen", Reason = RedirectReason.Manual });
            await db.SaveChangesAsync();
        }

        await UpdateEnglishAsync(isPublished: false, confirm: true, whenHidden: LanguageHiddenHandling.RedirectToDefaultLanguage);

        List<Redirect> rules = await RedirectsAsync();
        rules.Count(r => r.OldPath == "en/about").ShouldBe(1);
        rules.Single(r => r.OldPath == "en/about").NewPath.ShouldBe("/elle-secilen");
    }

    [Fact]
    public async Task Coming_back_removes_only_the_languages_own_temporary_rules_when_asked()
    {
        await SeedAsync();
        using (ApplicationDbContext db = CreateDb())
        {
            db.Redirects.Add(new Redirect { OldPath = "en/old-name", NewPath = "/en/about", Reason = RedirectReason.SlugChanged });
            await db.SaveChangesAsync();
        }
        await UpdateEnglishAsync(isPublished: false, confirm: true, whenHidden: LanguageHiddenHandling.RedirectToDefaultLanguage);

        Result<LanguageSaveResult> result = await UpdateEnglishAsync(isPublished: true, removeRedirects: true);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RedirectsRemoved.ShouldBe(3);
        List<Redirect> rules = await RedirectsAsync();
        rules.ShouldHaveSingleItem().OldPath.ShouldBe("en/old-name");
        _sitemaps.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Coming_back_keeps_the_rules_unless_asked()
    {
        await SeedAsync();
        await UpdateEnglishAsync(isPublished: false, confirm: true, whenHidden: LanguageHiddenHandling.RedirectToDefaultLanguage);

        await UpdateEnglishAsync(isPublished: true);

        (await RedirectsAsync()).Count.ShouldBe(3);
    }

    [Theory]
    [InlineData(1, "PageInfo.DefaultHomePageUnpublishNeedsConfirmation")]
    [InlineData(3, "PageInfo.HomePageUnpublishNeedsConfirmation")]
    public async Task Drafting_a_homepage_asks_for_confirmation_first(int pageId, string expectedCode)
    {
        await SeedAsync();

        using (ApplicationDbContext db = CreateDb())
        {
            Result refused = await new UpdatePageStatusCommandHandler(db).Handle(
                new UpdatePageStatusCommand(pageId, PageStatus.Draft), CancellationToken.None);
            refused.Error.Code.ShouldBe(expectedCode);
        }

        using (ApplicationDbContext db = CreateDb())
        {
            Result confirmed = await new UpdatePageStatusCommandHandler(db).Handle(
                new UpdatePageStatusCommand(pageId, PageStatus.Draft, ConfirmHomePageUnpublish: true), CancellationToken.None);
            confirmed.IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync();
        }

        using ApplicationDbContext read = CreateDb();
        (await read.PageInfos.SingleAsync(p => p.Id == pageId)).PageStatus.ShouldBe(PageStatus.Draft);
    }

    [Fact]
    public async Task Deleting_a_published_homepage_asks_for_confirmation_first()
    {
        await SeedAsync();
        using (ApplicationDbContext db = CreateDb())
        {
            // Childless, so only the homepage rule stands in the way.
            foreach (PageInfo child in await db.PageInfos.Where(p => p.ParentPageId == 3).ToListAsync())
                child.ParentPageId = null;
            await db.SaveChangesAsync();
        }

        using (ApplicationDbContext db = CreateDb())
        {
            Result refused = await new DeletePageCommandHandler(db).Handle(new DeletePageCommand(3), CancellationToken.None);
            refused.Error.ShouldBe(PageInfoErrors.HomePageUnpublishNeedsConfirmation);
        }

        using (ApplicationDbContext db = CreateDb())
        {
            Result confirmed = await new DeletePageCommandHandler(db).Handle(
                new DeletePageCommand(3, ConfirmHomePageUnpublish: true), CancellationToken.None);
            confirmed.IsSuccess.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task An_ordinary_page_needs_no_confirmation()
    {
        await SeedAsync();

        using ApplicationDbContext db = CreateDb();
        Result result = await new UpdatePageStatusCommandHandler(db).Handle(
            new UpdatePageStatusCommand(4, PageStatus.Draft), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    private sealed class SitemapRequests : ISitemapRegenerator
    {
        public int Count { get; private set; }
        public void RequestRegeneration() => Count++;
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
