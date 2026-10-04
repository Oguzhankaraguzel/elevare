using Application.Abstraction.Services.Authentication;
using Application.Features.Queries.ContentBulkEdits.SearchPagesByContent;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// Pins the invariant the handler now enforces: a page with MatchCount 0 is never
/// returned. Found live against real SQL Server, not reproducible here — the
/// candidate query's SQL <c>Contains</c> runs under the database's default
/// (case/accent-insensitive) collation, while <c>ApplyContentBulkEditCommandHandler</c>
/// and this handler's own <c>MatchCount</c> both count ordinal, exact-case
/// occurrences (the only kind a blind <see cref="string.Replace(string, string)"/> can
/// apply without risking unrelated text). A page whose only occurrence differed by
/// case slipped through the SQL filter and came back with MatchCount 0 — checked in
/// the UI, then silently skipped by Apply. EF Core's InMemory provider translates
/// <c>Contains</c> ordinally, so it can't reproduce the mismatch itself; this test
/// instead locks in the defensive filter so nothing removes it unnoticed.
/// </summary>
public sealed class SearchPagesByContentQueryHandlerTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new SearchTestUserContext());

    [Fact]
    public async Task A_page_matching_only_by_case_is_excluded_rather_than_shown_with_zero_matches()
    {
        using (ApplicationDbContext seed = CreateDb())
        {
            seed.Languages.Add(new Language
            {
                Id = 1,
                NameInNative = "Türkçe",
                NameInEnglish = "Turkish",
                TwoLetterCode = "tr",
                IsDefault = true,
                IsActive = true,
            });
            seed.PageInfos.Add(new PageInfo
            {
                Id = 1,
                Slug = "ilk-sayfa",
                FullSlug = "ilk-sayfa",
                LanguageId = 1,
                SeoMeta = new SeoMeta { Title = "Birebir Eşleşen" },
                Content = new PageContent { GjsHtml = "<p>aynı gün teslimat</p>" },
            });
            seed.PageInfos.Add(new PageInfo
            {
                Id = 2,
                Slug = "ikinci-sayfa",
                FullSlug = "ikinci-sayfa",
                LanguageId = 1,
                SeoMeta = new SeoMeta { Title = "Sadece Büyük Harfle Eşleşen" },
                Content = new PageContent { GjsHtml = "<p>Aynı gün teslimat</p>" },
            });
            await seed.SaveChangesAsync();
        }

        using ApplicationDbContext db = CreateDb();
        Result<List<PageContentMatchResponse>> result = await new SearchPagesByContentQueryHandler(db)
            .Handle(new SearchPagesByContentQuery("aynı gün teslimat"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value[0].PageInfoId.ShouldBe(1);
        result.Value[0].MatchCount.ShouldBe(1);
    }

    private sealed class SearchTestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
