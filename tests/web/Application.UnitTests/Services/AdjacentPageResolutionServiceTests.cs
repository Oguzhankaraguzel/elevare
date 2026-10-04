using Application.Features.Queries.Feeds.GetArticleFeed;
using Application.Services;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Covers what an article's neighbours give the public site: the "Önceki / Sonraki
/// Yazı" block (<see cref="AdjacentPageResolutionService"/>) and the article feed
/// (<see cref="GetArticleFeedQueryHandler"/>).
/// </summary>
public sealed class AdjacentPageResolutionServiceTests
{
    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();

    private const string Block = """
        <nav data-elevare-block="elevare-prevnext">
          <a data-elevare-adjacent="prev" href="#"><span data-elevare-field="title">Önceki</span></a>
          <a data-elevare-adjacent="next" href="#"><span data-elevare-field="title">Sonraki</span></a>
        </nav>
        """;

    public AdjacentPageResolutionServiceTests()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        db.Languages.Add(new PublicLanguage { Id = 1, TwoLetterCode = "tr", IsActive = true, IsPublished = true, IsDefault = true });
        db.PageInfos.AddRange(
            Page(1, "makaleler", null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), PublicPageKind.Category),
            Page(2, "makaleler/eski", 1, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Page(3, "makaleler/orta", 1, new DateTime(2023, 8, 13, 0, 0, 0, DateTimeKind.Utc)),
            Page(4, "makaleler/yeni", 1, new DateTime(2025, 5, 5, 0, 0, 0, DateTimeKind.Utc)),
            Page(5, "makaleler/taslak", 1, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), status: PublicPageStatus.Draft));
        db.SaveChanges();
    }

    private static PublicPage Page(int id, string fullSlug, int? parent, DateTime published,
        PublicPageKind kind = PublicPageKind.Article, PublicPageStatus status = PublicPageStatus.Published) => new()
        {
            Id = id, Slug = fullSlug.Split('/')[^1], FullSlug = fullSlug, ParentPageId = parent, LanguageId = 1,
            PageStatus = status, IsActive = true, Kind = kind, SeoTitle = "Başlık " + id, SeoMetaDescription = "Özet " + id,
            CreateDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            PublishedAt = DateTime.SpecifyKind(published, DateTimeKind.Utc),
        };

    private async Task<string> ResolveAsync(int pageId)
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        Result<string?> result = await new AdjacentPageResolutionService(db).ResolveAsync(Block, pageId, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value!;
    }

    [Fact]
    public async Task The_neighbours_by_publish_date_are_linked_and_drafts_skipped()
    {
        string html = await ResolveAsync(3);

        html.ShouldContain("href=\"/makaleler/eski\"");
        html.ShouldContain(">Başlık 2<");
        html.ShouldContain("href=\"/makaleler/yeni\"");
        html.ShouldContain(">Başlık 4<");
        html.ShouldNotContain("taslak");
    }

    [Fact]
    public async Task The_newest_post_has_no_next_and_keeps_its_previous_on_its_side()
    {
        string html = await ResolveAsync(4);

        html.ShouldContain("href=\"/makaleler/orta\"");
        html.ShouldNotContain("data-elevare-adjacent=\"next\"");
        html.ShouldContain("<span aria-hidden=\"true\"></span>");
    }

    [Fact]
    public async Task A_top_level_page_loses_the_block()
    {
        (await ResolveAsync(1)).ShouldNotContain("data-elevare-block=\"elevare-prevnext\"");
    }

    [Fact]
    public async Task The_feed_lists_published_articles_newest_first_with_absolute_links()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        Result<string> result = await new GetArticleFeedQueryHandler(db).Handle(
            new GetArticleFeedQuery("tr", "https://www.example.com", "/", "/feed.xml"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        string xml = result.Value;
        xml.ShouldContain("<rss version=\"2.0\"");
        xml.ShouldContain("<link>https://www.example.com/makaleler/yeni</link>");
        xml.IndexOf("makaleler/yeni", StringComparison.Ordinal).ShouldBeLessThan(xml.IndexOf("makaleler/orta", StringComparison.Ordinal));
        xml.ShouldNotContain("makaleler/taslak");
        // The category page is not an article.
        xml.ShouldNotContain("<link>https://www.example.com/makaleler</link>");
        xml.ShouldContain("<description>Özet 4</description>");
    }

    [Fact]
    public async Task A_feed_for_an_unknown_language_is_not_found()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        Result<string> result = await new GetArticleFeedQueryHandler(db).Handle(
            new GetArticleFeedQuery("xx", "https://www.example.com", "/xx", "/xx/feed.xml"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
    }
}
