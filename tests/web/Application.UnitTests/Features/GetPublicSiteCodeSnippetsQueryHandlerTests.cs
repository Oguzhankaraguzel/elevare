using Application.Features.Queries.SiteCodeSnippets.GetPublicSiteCodeSnippets;
using Domain.Entities.PublicSiteCodeSnippets;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Features;

/// <summary>
/// These snippets are third-party tags injected into every public page, so two
/// things must hold: a snippet switched off in the CMS really stops rendering, and
/// SortOrder really decides sequence — a consent banner that loads after the
/// trackers it is supposed to gate is worse than no banner at all.
/// </summary>
public sealed class GetPublicSiteCodeSnippetsQueryHandlerTests
{
    private const int HeadStart = 1;
    private const int BodyStart = 3;

    private static async Task<PublicReadDbContext> SeedAsync(params PublicSiteCodeSnippet[] snippets)
    {
        DbContextOptions<PublicReadDbContext> options = TestDbFactory.CreateOptions();
        using (PublicReadDbContext seed = TestDbFactory.Create(options))
        {
            seed.SiteCodeSnippets.AddRange(snippets);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        return TestDbFactory.Create(options);
    }

    private static Task<Result<Dictionary<int, List<PublicSiteCodeEntry>>>> RunAsync(PublicReadDbContext db) =>
        new GetPublicSiteCodeSnippetsQueryHandler(db, new NoOpCacheService())
            .Handle(new GetPublicSiteCodeSnippetsQuery(), CancellationToken.None);

    [Fact]
    public async Task Snippets_are_grouped_by_placement()
    {
        using PublicReadDbContext db = await SeedAsync(
            new PublicSiteCodeSnippet { Id = 1, Name = "GA4", Placement = HeadStart, Content = "<script>ga</script>", IsEnabled = true },
            new PublicSiteCodeSnippet { Id = 2, Name = "GTM noscript", Placement = BodyStart, Content = "<noscript>gtm</noscript>", IsEnabled = true });

        Result<Dictionary<int, List<PublicSiteCodeEntry>>> result = await RunAsync(db);

        result.IsSuccess.ShouldBeTrue();
        result.Value[HeadStart].Select(e => e.Content).ShouldBe(["<script>ga</script>"]);
        result.Value[BodyStart].Select(e => e.Content).ShouldBe(["<noscript>gtm</noscript>"]);
    }

    [Fact]
    public async Task SortOrder_decides_sequence_within_a_placement()
    {
        using PublicReadDbContext db = await SeedAsync(
            new PublicSiteCodeSnippet { Id = 1, Name = "Tracker", Placement = HeadStart, Content = "tracker", IsEnabled = true, SortOrder = 5 },
            new PublicSiteCodeSnippet { Id = 2, Name = "Consent", Placement = HeadStart, Content = "consent", IsEnabled = true, SortOrder = 0 });

        Result<Dictionary<int, List<PublicSiteCodeEntry>>> result = await RunAsync(db);

        result.Value[HeadStart].Select(e => e.Content).ShouldBe(["consent", "tracker"]);
    }

    [Fact]
    public async Task Entries_carry_the_snippet_id_so_a_page_can_leave_one_out()
    {
        // The layout filters by id against the page's exclusions
        // (PageInfoSiteCodeExclusions); an entry without its id could not be excluded.
        using PublicReadDbContext db = await SeedAsync(
            new PublicSiteCodeSnippet { Id = 7, Name = "Chat", Placement = HeadStart, Content = "<script>chat</script>", IsEnabled = true });

        Result<Dictionary<int, List<PublicSiteCodeEntry>>> result = await RunAsync(db);

        result.Value[HeadStart].ShouldBe([new PublicSiteCodeEntry(7, "<script>chat</script>")]);
    }

    [Fact]
    public async Task Disabled_snippets_are_not_rendered()
    {
        using PublicReadDbContext db = await SeedAsync(
            new PublicSiteCodeSnippet { Id = 1, Name = "Retired", Placement = HeadStart, Content = "<script>old</script>", IsEnabled = false });

        Result<Dictionary<int, List<PublicSiteCodeEntry>>> result = await RunAsync(db);

        result.Value.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Empty_snippets_do_not_produce_blank_entries(string? content)
    {
        // A half-created record must not add an empty string to the layout's loop —
        // that would emit stray whitespace into <head> on every page.
        using PublicReadDbContext db = await SeedAsync(
            new PublicSiteCodeSnippet { Id = 1, Name = "Yarim", Placement = HeadStart, Content = content, IsEnabled = true });

        Result<Dictionary<int, List<PublicSiteCodeEntry>>> result = await RunAsync(db);

        result.Value.ShouldBeEmpty();
    }
}
