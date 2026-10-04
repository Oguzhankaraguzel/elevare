using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Pages.CreatePage;
using Application.Features.Commands.Pages.UpdatePage;
using Domain.Entities.Languages;
using Domain.Entities.SiteCodeSnippets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// A page can switch individual site codes off for itself
/// (<c>PageInfoSiteCodeExclusions</c>). The save has the same three-way contract as
/// the page's tags: a list is the new truth, an empty list clears, and null — a
/// caller that does not know about exclusions — leaves them exactly as they were.
/// What lands in the join table is the test.
/// </summary>
public sealed class PageSiteCodeExclusionTests
{
    private const int TurkishId = 1;

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new TestUserContext());

    private async Task<int> SeedAsync()
    {
        using ApplicationDbContext db = CreateDb();
        db.Languages.Add(new Language
        {
            Id = TurkishId, NameInNative = "Türkçe", NameInEnglish = "Turkish",
            TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true,
        });
        db.SiteCodeSnippets.AddRange(
            new SiteCodeSnippet { Id = 10, Name = "GA4", Placement = SiteCodePlacement.HeadEnd, Content = "<script>ga</script>" },
            new SiteCodeSnippet { Id = 11, Name = "Chat", Placement = SiteCodePlacement.BodyEnd, Content = "<script>chat</script>" });
        await db.SaveChangesAsync();

        Result<int> created = await new CreatePageCommandHandler(db).Handle(
            new CreatePageCommand("Kampanya", "kampanya", TurkishId, null), CancellationToken.None);
        await db.SaveChangesAsync();
        created.IsSuccess.ShouldBeTrue();
        return created.Value;
    }

    private static UpdatePageSeoMeta BlankSeo() => new(
        IsCanonical: true, CanonicalUrl: null, MetaDescription: "", MetaAuthor: "",
        NoIndex: false, NoFollow: false, FocusKeyword: null, StructuredData: null,
        OgTitle: "", OgDescription: "", OgType: "website", OgImage: null, OgUrl: null,
        TwitterCard: null, TwitterSite: null);

    private async Task SaveAsync(int pageId, List<int>? excluded)
    {
        using ApplicationDbContext db = CreateDb();
        Result result = await new UpdatePageCommandHandler(db, new TestUserContext()).Handle(
            new UpdatePageCommand(pageId, "Kampanya", "kampanya", Domain.Entities.PageInfos.PageStatus.Draft,
                GjsHtml: null, GjsCss: null, GjsData: null, Seo: BlankSeo(),
                ExcludedSiteCodeSnippetIds: excluded),
            CancellationToken.None);
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : null);
        await db.SaveChangesAsync();
    }

    // Straight off the table rather than through GetPageById: that query also
    // includes the page's CreateUser, which the InMemory provider treats as an
    // inner join, and no user row exists in this fixture.
    private async Task<List<int>> ReadAsync(int pageId)
    {
        using ApplicationDbContext db = CreateDb();
        return await db.PageInfoSiteCodeExclusions
            .Where(x => x.PageInfoId == pageId)
            .Select(x => x.SiteCodeSnippetId)
            .ToListAsync();
    }

    [Fact]
    public async Task Saved_exclusions_come_back_with_the_page()
    {
        int pageId = await SeedAsync();

        await SaveAsync(pageId, [10, 11]);

        (await ReadAsync(pageId)).ShouldBe([10, 11], ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_list_clears_them_and_null_leaves_them_alone()
    {
        int pageId = await SeedAsync();
        await SaveAsync(pageId, [11]);

        await SaveAsync(pageId, null);
        (await ReadAsync(pageId)).ShouldBe([11]);

        await SaveAsync(pageId, []);
        (await ReadAsync(pageId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_id_that_is_not_a_snippet_is_ignored_rather_than_failing_the_save()
    {
        // The dialog only offers real snippets, but one can be purged between the
        // page being opened and saved; the save must not fall over on it.
        int pageId = await SeedAsync();

        await SaveAsync(pageId, [10, 999]);

        (await ReadAsync(pageId)).ShouldBe([10]);
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
