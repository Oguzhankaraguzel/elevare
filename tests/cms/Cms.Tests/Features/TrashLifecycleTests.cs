using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Pages.CreatePageTranslation;
using Application.Features.Commands.Pages.DeletePage;
using Application.Features.Commands.Trash.EmptyTrash;
using Application.Features.Commands.Trash.PurgeTrashItem;
using Application.Features.Commands.Trash.RestoreTrashItem;
using Application.Features.Queries.Trash.GetTrashedItems;
using Domain.Entities.SiteCodeSnippets;
using Application.Features.Queries.Pages.GetDeletedTranslation;
using Application.Features.Trash;
using Domain.Entities.FormSubmissions;
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
/// The Trash used to have no exit: every delete left a row forever, and deleting
/// then recreating the same page piled up tombstones that could never be restored
/// because a live page had taken the address back. These cover the three rules that
/// close that hole — one tombstone per address, restore-or-replace on recreate, and
/// a permanent delete that refuses to take visitor data with it.
/// </summary>
public sealed class TrashLifecycleTests
{
    private const int TurkishId = 1;
    private const int EnglishId = 2;

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new TrashTestUserContext());

    private async Task<int> SeedSourceAsync(string slug)
    {
        using ApplicationDbContext db = CreateDb();
        var turkish = new Language
        {
            Id = TurkishId,
            NameInNative = "Turkce",
            NameInEnglish = "Turkish",
            TwoLetterCode = "tr",
            IsDefault = true,
            IsActive = true,
            IsPublished = true,
        };
        db.Languages.AddRange(turkish, new Language
        {
            Id = EnglishId,
            NameInNative = "English",
            NameInEnglish = "English",
            TwoLetterCode = "en",
            IsActive = true,
            IsPublished = true,
        });

        var page = new PageInfo
        {
            Slug = slug,
            PageStatus = PageStatus.Published,
            LanguageId = TurkishId,
            Language = turkish,
            SeoMeta = new SeoMeta { Title = slug },
            Content = new PageContent(),
        };
        page.ComputeFullSlug("tr");
        db.PageInfos.Add(page);
        await db.SaveChangesAsync(CancellationToken.None);
        return page.Id;
    }

    private async Task<Result<int>> CreateTranslationAsync(int sourceId, bool replaceDeleted = false)
    {
        using ApplicationDbContext db = CreateDb();
        return await new CreatePageTranslationCommandHandler(db)
            .Handle(new CreatePageTranslationCommand(sourceId, EnglishId, replaceDeleted), CancellationToken.None);
    }

    private async Task DeleteAsync(int pageId, string? redirectTo = null)
    {
        using ApplicationDbContext db = CreateDb();
        await new DeletePageCommandHandler(db)
            .Handle(new DeletePageCommand(pageId, redirectTo), CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<int> TombstoneCountAsync()
    {
        using ApplicationDbContext db = CreateDb();
        return await db.PageInfos
            .IgnoreQueryFilters()
            .CountAsync(p => p.IsDeleted && p.LanguageId == EnglishId, CancellationToken.None);
    }

    private async Task AddSubmissionAsync(int pageId)
    {
        using ApplicationDbContext db = CreateDb();
        db.FormSubmissions.Add(new FormSubmission
        {
            PageInfoId = pageId,
            FormName = "iletisim",
            FieldsJson = "{}",
            SubmittedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(CancellationToken.None);
    }

    // ── A: one tombstone per address ──────────────────────────────────────

    [Fact]
    public async Task Repeated_delete_and_recreate_leaves_only_the_last_deleted_version()
    {
        // Four rounds of the loop that produced four unrestorable rows before.
        int sourceId = await SeedSourceAsync("maintenance");

        for (int i = 0; i < 4; i++)
        {
            Result<int> created = await CreateTranslationAsync(sourceId);
            created.IsSuccess.ShouldBeTrue();
            await DeleteAsync(created.Value);
        }

        (await TombstoneCountAsync()).ShouldBe(1);
    }

    // ── B: restore-or-replace instead of a silent duplicate ───────────────

    [Fact]
    public async Task A_deleted_translation_is_offered_back_before_a_new_one_is_made()
    {
        int sourceId = await SeedSourceAsync("hakkimizda");
        Result<int> first = await CreateTranslationAsync(sourceId);
        await DeleteAsync(first.Value);

        using ApplicationDbContext db = CreateDb();
        Result<DeletedTranslationResponse?> found = await new GetDeletedTranslationQueryHandler(db)
            .Handle(new GetDeletedTranslationQuery(sourceId, EnglishId), CancellationToken.None);

        found.Value.ShouldNotBeNull();
        found.Value!.Id.ShouldBe(first.Value);
        found.Value.DeletedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Nothing_is_offered_back_when_the_language_was_never_translated()
    {
        int sourceId = await SeedSourceAsync("iletisim");

        using ApplicationDbContext db = CreateDb();
        Result<DeletedTranslationResponse?> found = await new GetDeletedTranslationQueryHandler(db)
            .Handle(new GetDeletedTranslationQuery(sourceId, EnglishId), CancellationToken.None);

        found.Value.ShouldBeNull();
    }

    [Fact]
    public async Task Choosing_to_start_over_removes_the_tombstone_it_replaces()
    {
        // Leaving it would only offer a Restore button that can never succeed, since
        // the new page now holds the address.
        int sourceId = await SeedSourceAsync("blog");
        Result<int> first = await CreateTranslationAsync(sourceId);
        await DeleteAsync(first.Value);

        Result<int> replacement = await CreateTranslationAsync(sourceId, replaceDeleted: true);

        replacement.IsSuccess.ShouldBeTrue();
        (await TombstoneCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Starting_over_keeps_the_original_slug_rather_than_suffixing_it()
    {
        int sourceId = await SeedSourceAsync("blog");
        Result<int> first = await CreateTranslationAsync(sourceId);
        await DeleteAsync(first.Value);

        Result<int> replacement = await CreateTranslationAsync(sourceId, replaceDeleted: true);

        using ApplicationDbContext db = CreateDb();
        PageInfo page = await db.PageInfos.SingleAsync(p => p.Id == replacement.Value, CancellationToken.None);
        page.Slug.ShouldBe("blog");
        page.FullSlug.ShouldBe("en/blog");
    }

    // ── C: permanent delete, and what it refuses ─────────────────────────

    [Fact]
    public async Task Purging_a_page_that_holds_form_submissions_is_refused()
    {
        int sourceId = await SeedSourceAsync("iletisim");
        Result<int> translation = await CreateTranslationAsync(sourceId);
        await DeleteAsync(translation.Value);
        await AddSubmissionAsync(translation.Value);

        using ApplicationDbContext db = CreateDb();
        Result result = await new PurgeTrashItemCommandHandler(db)
            .Handle(new PurgeTrashItemCommand(TrashEntityType.PageInfo, translation.Value), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(TrashErrors.CannotPurgeHasFormSubmissions.Code);
        (await TombstoneCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task A_trashed_page_with_nothing_depending_on_it_can_be_purged()
    {
        int sourceId = await SeedSourceAsync("kampanya");
        Result<int> translation = await CreateTranslationAsync(sourceId);
        await DeleteAsync(translation.Value);

        using ApplicationDbContext db = CreateDb();
        Result result = await new PurgeTrashItemCommandHandler(db)
            .Handle(new PurgeTrashItemCommand(TrashEntityType.PageInfo, translation.Value), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await TombstoneCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Emptying_the_trash_reports_what_it_had_to_leave_behind()
    {
        // One protected page must not stop the rest from being cleared, and the count
        // has to say so — "emptied" with rows still on screen reads as a broken button.
        int sourceId = await SeedSourceAsync("iletisim");

        Result<int> protectedPage = await CreateTranslationAsync(sourceId);
        await DeleteAsync(protectedPage.Value);
        await AddSubmissionAsync(protectedPage.Value);

        using ApplicationDbContext db = CreateDb();
        Result<EmptyTrashResult> result = await new EmptyTrashCommandHandler(db)
            .Handle(new EmptyTrashCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Skipped.ShouldBe(1);
        (await TombstoneCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task A_redirect_that_followed_a_purged_page_survives_on_its_stored_target()
    {
        // The page is gone, but anyone holding its old URL still has to land
        // somewhere — so the rule is unbound, not deleted.
        int sourceId = await SeedSourceAsync("kampanya");
        Result<int> translation = await CreateTranslationAsync(sourceId);

        using (ApplicationDbContext publish = CreateDb())
        {
            PageInfo page = await publish.PageInfos.SingleAsync(p => p.Id == translation.Value, CancellationToken.None);
            page.PageStatus = PageStatus.Published;
            await publish.SaveChangesAsync(CancellationToken.None);
        }

        await DeleteAsync(translation.Value, "/yeni-kampanya");

        using ApplicationDbContext db = CreateDb();
        Result purged = await new PurgeTrashItemCommandHandler(db)
            .Handle(new PurgeTrashItemCommand(TrashEntityType.PageInfo, translation.Value), CancellationToken.None);
        purged.IsSuccess.ShouldBeTrue();

        Redirect redirect = await db.Redirects.IgnoreQueryFilters().SingleAsync(CancellationToken.None);
        redirect.NewPath.ShouldBe("/yeni-kampanya");
        redirect.SourcePageId.ShouldBeNull();
    }

    [Fact]
    public async Task A_restored_site_code_comes_back_switched_off()
    {
        // The rule this pins down: a site code is the only trashed thing that starts
        // executing again in every visitor's browser the moment it returns. Restoring
        // the row and re-serving it are two decisions, so restore only does the first.
        int id;
        using (ApplicationDbContext seed = CreateDb())
        {
            var snippet = new SiteCodeSnippet { Name = "Analytics", Content = "<script></script>", IsEnabled = true };
            seed.SiteCodeSnippets.Add(snippet);
            await seed.SaveChangesAsync(CancellationToken.None);
            seed.SiteCodeSnippets.Remove(snippet);
            await seed.SaveChangesAsync(CancellationToken.None);
            id = snippet.Id;
        }

        using ApplicationDbContext db = CreateDb();
        Result restored = await new RestoreTrashItemCommandHandler(db)
            .Handle(new RestoreTrashItemCommand(TrashEntityType.SiteCodeSnippet, id), CancellationToken.None);

        restored.IsSuccess.ShouldBeTrue();
        SiteCodeSnippet back = await db.SiteCodeSnippets.IgnoreQueryFilters().SingleAsync(s => s.Id == id, CancellationToken.None);
        back.IsDeleted.ShouldBeFalse();
        back.IsEnabled.ShouldBeFalse();
        back.Content.ShouldBe("<script></script>");
    }

    [Fact]
    public async Task A_restored_site_code_keeps_the_position_it_had()
    {
        // SortOrder is deliberately not renumbered on restore: renumbering would move
        // rows the author never touched, and a duplicate value still orders stably
        // because both the admin list and the public render tie-break on Id.
        int id;
        using (ApplicationDbContext seed = CreateDb())
        {
            var snippet = new SiteCodeSnippet { Name = "Chat", Content = "<script></script>", SortOrder = 30 };
            seed.SiteCodeSnippets.Add(snippet);
            await seed.SaveChangesAsync(CancellationToken.None);
            seed.SiteCodeSnippets.Remove(snippet);
            await seed.SaveChangesAsync(CancellationToken.None);
            id = snippet.Id;
        }

        using ApplicationDbContext db = CreateDb();
        await new RestoreTrashItemCommandHandler(db)
            .Handle(new RestoreTrashItemCommand(TrashEntityType.SiteCodeSnippet, id), CancellationToken.None);

        SiteCodeSnippet back = await db.SiteCodeSnippets.IgnoreQueryFilters().SingleAsync(s => s.Id == id, CancellationToken.None);
        back.SortOrder.ShouldBe(30);
    }

    [Fact]
    public async Task A_trashed_site_code_is_listed_in_the_trash()
    {
        using (ApplicationDbContext seed = CreateDb())
        {
            var snippet = new SiteCodeSnippet { Name = "Meta Pixel", Content = "<script></script>" };
            seed.SiteCodeSnippets.Add(snippet);
            await seed.SaveChangesAsync(CancellationToken.None);
            seed.SiteCodeSnippets.Remove(snippet);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        using ApplicationDbContext db = CreateDb();
        Result<List<TrashItemResponse>> items = await new GetTrashedItemsQueryHandler(db)
            .Handle(new GetTrashedItemsQuery(), CancellationToken.None);

        items.IsSuccess.ShouldBeTrue();
        TrashItemResponse row = items.Value.ShouldHaveSingleItem();
        row.EntityType.ShouldBe(TrashEntityType.SiteCodeSnippet);
        row.DisplayName.ShouldBe("Meta Pixel");
    }

    private sealed class TrashTestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
