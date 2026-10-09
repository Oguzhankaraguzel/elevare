using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Redirects;
using Application.Features.Commands.Redirects.CreateRedirect;
using Application.Features.Commands.Redirects.UpdateRedirect;
using Application.Features.Queries.Redirects.GetRedirects;
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
/// The rules a redirect has to obey, and the diagnosis the management screen shows.
/// Both matter for the same reason: a wrong redirect is invisible in the CMS and
/// total on the public side — the visitor either loops or lands on a 404.
/// </summary>
public sealed class RedirectRulesTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new RedirectTestUserContext());

    private async Task SeedAsync(PageInfo[] pages, Redirect[] redirects)
    {
        using ApplicationDbContext db = CreateDb();
        var turkish = new Language
        {
            Id = 1,
            NameInNative = "Türkçe",
            NameInEnglish = "Turkish",
            TwoLetterCode = "tr",
            IsDefault = true,
            IsActive = true,
            IsPublished = true,
        };
        db.Languages.Add(turkish);

        foreach (PageInfo page in pages)
        {
            page.LanguageId = 1;
            page.Language = turkish;
            db.PageInfos.Add(page);
        }

        db.Redirects.AddRange(redirects);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static PageInfo LivePage(int id, string slug) => new()
    {
        Id = id,
        Slug = slug,
        FullSlug = slug,
        PageStatus = PageStatus.Published,
        SeoMeta = new SeoMeta { Title = slug },
        Content = new PageContent(),
    };

    private async Task<Result<int>> CreateAsync(string oldPath, string? newPath)
    {
        using ApplicationDbContext db = CreateDb();
        return await new CreateRedirectCommandHandler(db)
            .Handle(new CreateRedirectCommand(oldPath, newPath), CancellationToken.None);
    }

    private async Task<List<RedirectListItemResponse>> ListAsync()
    {
        using ApplicationDbContext db = CreateDb();
        Result<List<RedirectListItemResponse>> result = await new GetRedirectsQueryHandler(db)
            .Handle(new GetRedirectsQuery(), CancellationToken.None);
        return result.Value;
    }

    // ── Redirect type ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_hand_written_rule_keeps_the_chosen_type(bool temporary)
    {
        await SeedAsync([LivePage(1, "yeni")], []);

        Result<int> created;
        using (ApplicationDbContext db = CreateDb())
        {
            created = await new CreateRedirectCommandHandler(db)
                .Handle(new CreateRedirectCommand("eski", "/yeni", temporary), CancellationToken.None);
        }

        created.IsSuccess.ShouldBeTrue();
        (await ListAsync()).Single().IsTemporary.ShouldBe(temporary);
    }

    [Fact]
    public async Task Editing_a_rule_can_change_its_type()
    {
        await SeedAsync([LivePage(1, "yeni")], [new Redirect { Id = 7, OldPath = "eski", NewPath = "/yeni" }]);

        using (ApplicationDbContext db = CreateDb())
        {
            Result result = await new UpdateRedirectCommandHandler(db)
                .Handle(new UpdateRedirectCommand(7, "eski", "/yeni", IsTemporary: true), CancellationToken.None);
            result.IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync(CancellationToken.None);
        }

        (await ListAsync()).Single().IsTemporary.ShouldBeTrue();
    }

    // ── Save-time rules ───────────────────────────────────────────────────

    [Fact]
    public async Task A_rule_sitting_on_a_live_page_is_refused()
    {
        // Allowing it would make that page unreachable with no clue anywhere in the CMS.
        await SeedAsync([LivePage(1, "hizmetlerimiz")], []);

        Result<int> result = await CreateAsync("hizmetlerimiz", "/iletisim");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RedirectErrors.ShadowsLivePage.Code);
    }

    [Fact]
    public async Task A_rule_that_would_close_a_loop_is_refused()
    {
        await SeedAsync([], [new Redirect { OldPath = "b", NewPath = "/a", Reason = RedirectReason.Manual }]);

        // a -> b already goes b -> a, so this closes the circle.
        Result<int> result = await CreateAsync("a", "/b");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RedirectErrors.CircularRedirect.Code);
    }

    [Fact]
    public async Task The_same_source_path_cannot_be_redirected_twice()
    {
        await SeedAsync([], [new Redirect { OldPath = "eski", NewPath = "/yeni", Reason = RedirectReason.Manual }]);

        Result<int> result = await CreateAsync("eski", "/baska");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RedirectErrors.OldPathAlreadyExists.Code);
    }

    [Theory]
    [InlineData("/eski-sayfa", "eski-sayfa")]
    [InlineData("eski-sayfa/", "eski-sayfa")]
    [InlineData("  /eski-sayfa/  ", "eski-sayfa")]
    public async Task The_source_path_is_stored_in_one_shape_whatever_was_typed(string typed, string stored)
    {
        // Without this the duplicate check misses duplicates and the public site,
        // which compares against bare FullSlugs, never matches the rule at all.
        await SeedAsync([], []);

        (await CreateAsync(typed, "/yeni")).IsSuccess.ShouldBeTrue();

        using ApplicationDbContext db = CreateDb();
        (await db.Redirects.SingleAsync(CancellationToken.None)).OldPath.ShouldBe(stored);
    }

    [Fact]
    public async Task An_internal_target_gets_a_leading_slash_and_an_external_one_is_left_alone()
    {
        await SeedAsync([], []);

        (await CreateAsync("a", "yeni-sayfa")).IsSuccess.ShouldBeTrue();
        (await CreateAsync("b", "https://baska-site.com/x")).IsSuccess.ShouldBeTrue();

        using ApplicationDbContext db = CreateDb();
        (await db.Redirects.SingleAsync(r => r.OldPath == "a", CancellationToken.None)).NewPath.ShouldBe("/yeni-sayfa");
        (await db.Redirects.SingleAsync(r => r.OldPath == "b", CancellationToken.None)).NewPath.ShouldBe("https://baska-site.com/x");
    }

    [Fact]
    public async Task An_empty_target_is_stored_as_410_Gone_rather_than_an_empty_string()
    {
        await SeedAsync([], []);

        (await CreateAsync("kaldirildi", "   ")).IsSuccess.ShouldBeTrue();

        using ApplicationDbContext db = CreateDb();
        (await db.Redirects.SingleAsync(CancellationToken.None)).NewPath.ShouldBeNull();
    }

    // ── Diagnosis shown on the screen ─────────────────────────────────────

    [Fact]
    public async Task A_rule_pointing_at_a_published_page_is_reported_healthy()
    {
        await SeedAsync(
            [LivePage(1, "yeni-sayfa")],
            [new Redirect { OldPath = "eski-sayfa", NewPath = "/yeni-sayfa", Reason = RedirectReason.Manual }]);

        (await ListAsync()).Single().Health.ShouldBe(RedirectHealth.None);
    }

    [Fact]
    public async Task A_rule_pointing_at_nothing_is_reported_as_a_dead_target()
    {
        await SeedAsync(
            [],
            [new Redirect { OldPath = "eski-sayfa", NewPath = "/silinmis", Reason = RedirectReason.Manual }]);

        (await ListAsync()).Single().Health.HasFlag(RedirectHealth.BrokenTarget).ShouldBeTrue();
    }

    [Fact]
    public async Task A_two_hop_chain_is_reported_with_its_length()
    {
        // a -> b -> c, where c is real. It works, but every visitor pays two round
        // trips and search engines stop following long before this grows further.
        await SeedAsync(
            [LivePage(1, "c")],
            [
                new Redirect { OldPath = "a", NewPath = "/b", Reason = RedirectReason.Manual },
                new Redirect { OldPath = "b", NewPath = "/c", Reason = RedirectReason.Manual },
            ]);

        RedirectListItemResponse first = (await ListAsync()).Single(r => r.OldPath == "a");

        first.Health.HasFlag(RedirectHealth.Chained).ShouldBeTrue();
        first.Health.HasFlag(RedirectHealth.BrokenTarget).ShouldBeFalse();
        first.ChainLength.ShouldBe(2);
    }

    [Fact]
    public async Task An_existing_loop_is_reported_rather_than_hanging()
    {
        // Rules like these can only get in through an import or a direct DB edit,
        // but the screen still has to survive them and name the problem.
        await SeedAsync(
            [],
            [
                new Redirect { OldPath = "a", NewPath = "/b", Reason = RedirectReason.Manual },
                new Redirect { OldPath = "b", NewPath = "/a", Reason = RedirectReason.Manual },
            ]);

        (await ListAsync()).ShouldAllBe(r => r.Health.HasFlag(RedirectHealth.Loop));
    }

    [Fact]
    public async Task A_rule_with_no_target_is_reported_as_gone_not_broken()
    {
        await SeedAsync([], [new Redirect { OldPath = "kaldirildi", NewPath = null, Reason = RedirectReason.Manual }]);

        RedirectListItemResponse item = (await ListAsync()).Single();

        item.Health.ShouldBe(RedirectHealth.Gone);
        item.Health.HasFlag(RedirectHealth.BrokenTarget).ShouldBeFalse();
    }

    // The default language's homepage has the FullSlug "" — a real target the site
    // answers as "/". Treated as "no target", every rule pointing home (a language
    // taken off the site, a default-language switch) showed up as a 410.
    [Fact]
    public async Task A_rule_bound_to_the_default_homepage_is_reported_healthy()
    {
        PageInfo home = LivePage(1, "home");
        home.FullSlug = "";
        await SeedAsync([home],
            [new Redirect { OldPath = "en", NewPath = "/", SourcePageId = 1, Reason = RedirectReason.LanguageUnpublished, IsTemporary = true }]);

        (await ListAsync()).Single().Health.ShouldBe(RedirectHealth.None);
    }

    [Fact]
    public async Task An_external_target_is_marked_external_and_never_called_broken()
    {
        await SeedAsync([], [new Redirect { OldPath = "kampanya", NewPath = "https://baska-site.com/x", Reason = RedirectReason.Manual }]);

        RedirectListItemResponse item = (await ListAsync()).Single();

        item.Health.ShouldBe(RedirectHealth.External);
    }

    [Fact]
    public async Task A_bound_rule_resolves_to_the_pages_current_address_not_the_stored_one()
    {
        // The point of binding: the page was renamed after the rule was written, and
        // the visitor still has to arrive at where it lives now.
        await SeedAsync(
            [LivePage(7, "ucuncu-ad")],
            [new Redirect { OldPath = "ilk-ad", NewPath = "/ikinci-ad", SourcePageId = 7, Reason = RedirectReason.SlugChanged }]);

        RedirectListItemResponse item = (await ListAsync()).Single();

        item.ResolvedTarget.ShouldBe("ucuncu-ad");
        item.Health.ShouldBe(RedirectHealth.None);
    }

    [Fact]
    public async Task Path_shape_differences_do_not_make_a_working_rule_look_broken()
    {
        // OldPath is stored bare, a typed NewPath carries a leading slash. Comparing
        // them without normalising is how a correct rule ends up flagged.
        await SeedAsync(
            [LivePage(1, "en/about-us")],
            [new Redirect { OldPath = "en/hakkimizda", NewPath = "/en/about-us/", Reason = RedirectReason.Manual }]);

        (await ListAsync()).Single().Health.ShouldBe(RedirectHealth.None);
    }

    private sealed class RedirectTestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
