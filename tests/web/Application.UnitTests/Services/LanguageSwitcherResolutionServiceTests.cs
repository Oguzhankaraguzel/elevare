using Application.Services;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Covers <see cref="LanguageSwitcherResolutionService"/> — the request-time pass that
/// points each language-switcher link at the CURRENT page's counterpart rather than at
/// a fixed URL chosen when the block was dropped into the layout.
/// <para>
/// The fallback chain is the interesting part: a page with no translation in the
/// target language must send the visitor to that language's homepage instead of
/// nowhere, because a dead switcher link looks like a broken site.
/// </para>
/// </summary>
public sealed class LanguageSwitcherResolutionServiceTests
{
    private const int TurkishId = 1;
    private const int EnglishId = 2;

    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();

    public LanguageSwitcherResolutionServiceTests()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        db.Languages.AddRange(
            new PublicLanguage { Id = TurkishId, TwoLetterCode = "tr", IsActive = true, IsPublished = true, IsDefault = true },
            new PublicLanguage { Id = EnglishId, TwoLetterCode = "en", IsActive = true, IsPublished = true });

        // A TR page and its EN sibling, linked by the shared PageGroup.
        db.PageInfos.AddRange(
            NewPage(id: 10, slug: "hakkimizda", fullSlug: "hakkimizda", languageId: TurkishId, pageGroupId: 100),
            NewPage(id: 11, slug: "about-us", fullSlug: "about-us", languageId: EnglishId, pageGroupId: 100),
            // A TR-only page with no EN counterpart, plus the EN homepage to fall back to.
            NewPage(id: 20, slug: "sadece-tr", fullSlug: "sadece-tr", languageId: TurkishId, pageGroupId: 200),
            NewPage(id: 30, slug: "home", fullSlug: "home", languageId: EnglishId, pageGroupId: 300));

        db.SaveChanges();
    }

    private static PublicPage NewPage(int id, string slug, string fullSlug, int languageId, int pageGroupId) => new()
    {
        Id = id,
        Slug = slug,
        FullSlug = fullSlug,
        LanguageId = languageId,
        PageGroupId = pageGroupId,
        PageStatus = PublicPageStatus.Published,
        IsActive = true,
        SeoTitle = slug,
        SeoMetaDescription = "",
    };

    private async Task<string?> ResolveAsync(string? html, int pageId)
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        var service = new LanguageSwitcherResolutionService(db);

        Result<string?> result = await service.ResolveAsync(html, pageId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Html_with_nothing_in_it_passes_straight_through(string? html)
    {
        string? resolved = await ResolveAsync(html, 10);

        resolved.ShouldBe(html);
    }

    [Fact]
    public async Task Html_without_a_switcher_is_returned_unchanged_in_substance()
    {
        string? resolved = await ResolveAsync("<p>merhaba</p>", 10);

        resolved.ShouldNotBeNull();
        resolved.ShouldContain("merhaba");
    }

    [Fact]
    public async Task Each_slot_points_at_that_language_version_of_the_current_page()
    {
        string? resolved = await ResolveAsync(
            """
            <a data-elevare-lang-slot="tr" href="#">TR</a>
            <a data-elevare-lang-slot="en" href="#">EN</a>
            """,
            pageId: 10);

        resolved.ShouldNotBeNull();
        resolved.ShouldContain("href=\"/hakkimizda\"");
        resolved.ShouldContain("href=\"/about-us\"");
    }

    [Fact]
    public async Task A_page_with_no_counterpart_falls_back_to_that_language_homepage()
    {
        // Without this fallback the EN link on a Turkish-only page would stay at "#",
        // which reads to a visitor as a broken switcher rather than as missing content.
        string? resolved = await ResolveAsync(
            """<a data-elevare-lang-slot="en" href="#">EN</a>""",
            pageId: 20);

        resolved.ShouldNotBeNull();
        resolved.ShouldContain("href=\"/home\"");
    }

    [Fact]
    public async Task A_slot_for_a_language_that_is_not_publicly_visible_is_removed()
    {
        // This used to be left alone, which meant it shipped as the href="#" the
        // block was authored with: a switcher entry a visitor can click and that
        // goes nowhere, and a link a crawler sees as empty. It happens whenever a
        // language is deleted or unpublished after a page was built — and it used to
        // happen at build time too, because the editor offered every ACTIVE language
        // while only published ones are ever resolved here.
        string? resolved = await ResolveAsync(
            """
            <ul>
                <li><a data-elevare-lang-slot="tr" href="#">TR</a></li>
                <li><a data-elevare-lang-slot="de" href="#">DE</a></li>
            </ul>
            """,
            pageId: 10);

        resolved.ShouldNotBeNull();
        resolved.ShouldContain("href=\"/hakkimizda\"");
        resolved.ShouldNotContain("data-elevare-lang-slot=\"de\"");
        resolved.ShouldNotContain("href=\"#\"");
        // The whole row goes, not just the anchor: an empty <li> would still be a
        // bullet on the page and an item a screen reader counts and announces.
        resolved.ShouldNotContain("<li></li>");
        System.Text.RegularExpressions.Regex.Count(resolved, "<li").ShouldBe(1);
    }

    [Fact]
    public async Task An_unknown_page_id_leaves_every_slot_untouched()
    {
        // Deliberately NOT the removal above: nothing resolved because the lookup
        // itself failed, which says nothing about any individual language. Stripping
        // the switcher on a bad page id would turn a transient data problem into a
        // page that quietly lost its language navigation.
        string? resolved = await ResolveAsync(
            """<a data-elevare-lang-slot="tr" href="#">TR</a>""",
            pageId: 9999);

        resolved.ShouldNotBeNull();
        resolved.ShouldContain("href=\"#\"");
    }
}
