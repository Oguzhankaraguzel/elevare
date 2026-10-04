using Application.Features.Queries.SiteCodeSnippets.GetPageSiteCodeExclusions;
using Domain.Entities.PublicSiteCodeSnippets;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Features;

/// <summary>
/// A page's exclusions are its own: the ids for one page, nothing from another,
/// and an empty set — not a failure — for a page that never switched anything off,
/// since that is what every page is until its author says otherwise.
/// </summary>
public sealed class GetPageSiteCodeExclusionsQueryHandlerTests
{
    private static async Task<PublicReadDbContext> SeedAsync(params PublicPageInfoSiteCodeExclusion[] rows)
    {
        DbContextOptions<PublicReadDbContext> options = TestDbFactory.CreateOptions();
        using (PublicReadDbContext seed = TestDbFactory.Create(options))
        {
            seed.PageInfoSiteCodeExclusions.AddRange(rows);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        return TestDbFactory.Create(options);
    }

    [Fact]
    public async Task Returns_only_the_ids_switched_off_on_that_page()
    {
        using PublicReadDbContext db = await SeedAsync(
            new PublicPageInfoSiteCodeExclusion { PageInfoId = 2, SiteCodeSnippetId = 7 },
            new PublicPageInfoSiteCodeExclusion { PageInfoId = 2, SiteCodeSnippetId = 9 },
            new PublicPageInfoSiteCodeExclusion { PageInfoId = 3, SiteCodeSnippetId = 7 });

        Result<HashSet<int>> result = await new GetPageSiteCodeExclusionsQueryHandler(db)
            .Handle(new GetPageSiteCodeExclusionsQuery(2), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe([7, 9], ignoreOrder: true);
    }

    [Fact]
    public async Task A_page_with_no_exclusions_gets_an_empty_set()
    {
        using PublicReadDbContext db = await SeedAsync(
            new PublicPageInfoSiteCodeExclusion { PageInfoId = 3, SiteCodeSnippetId = 7 });

        Result<HashSet<int>> result = await new GetPageSiteCodeExclusionsQueryHandler(db)
            .Handle(new GetPageSiteCodeExclusionsQuery(2), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }
}
