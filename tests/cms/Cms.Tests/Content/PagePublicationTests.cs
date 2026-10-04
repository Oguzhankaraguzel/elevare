using Application.Features.Commands.Pages.Shared;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Shouldly;

namespace Cms.Tests.Content;

/// <summary>
/// Covers <see cref="PagePublication"/>: the one date listings, structured data and
/// share tags publish a page under.
/// </summary>
public sealed class PagePublicationTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    private static PageInfo Page(PageStatus status, string? html = null, DateTime? publishedAt = null) => new()
    {
        Slug = "yazi", FullSlug = "yazi", LanguageId = 1, PageStatus = status, PublishedAt = publishedAt,
        Content = html is null ? null : new PageContent { GjsHtml = html },
    };

    // As found on a real page: the day slipped outside <time>, the attribute kept the block's default.
    private const string Article = """
        <article data-elevare-article><header>
          <p>Yazar: Ada · 13<time data-elevare-article-date datetime="2026-01-01"> Ağustos 2023</time></p>
        </header></article>
        """;

    [Fact]
    public void A_page_is_published_the_first_time_it_goes_live()
    {
        PageInfo page = Page(PageStatus.Published);

        PagePublication.Stamp(page, Now);

        page.PublishedAt.ShouldBe(Now);
    }

    [Fact]
    public void Going_live_again_does_not_make_an_old_page_new()
    {
        DateTime first = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        PageInfo page = Page(PageStatus.Published, publishedAt: first);

        PagePublication.Stamp(page, Now);

        page.PublishedAt.ShouldBe(first);
    }

    [Fact]
    public void A_draft_has_no_publish_date()
    {
        PageInfo page = Page(PageStatus.Draft);

        PagePublication.Stamp(page, Now);

        page.PublishedAt.ShouldBeNull();
    }

    [Fact]
    public void The_date_an_article_block_shows_wins_even_over_an_earlier_publish()
    {
        PageInfo page = Page(PageStatus.Published, Article, publishedAt: new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc));

        PagePublication.Stamp(page, Now);

        page.PublishedAt.ShouldBe(new DateTime(2023, 8, 13, 0, 0, 0, DateTimeKind.Utc));
    }
}
