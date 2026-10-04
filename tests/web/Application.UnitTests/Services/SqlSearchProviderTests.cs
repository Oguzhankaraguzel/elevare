using Application.Features.Queries.Search.SearchPublicContent;
using Application.Services;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Covers <see cref="SqlSearchProvider"/> — the default <c>ISearchProvider</c>
/// behind the "Arama Kutusu" block: matching against title/slug AND visible page
/// content (not raw markup), excluding results whose only "match" is inside HTML
/// markup (e.g. a class name), category resolution via the page hierarchy's
/// immediate parent, and the Published/active/language gates.
/// </summary>
public sealed class SqlSearchProviderTests
{
    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();

    public SqlSearchProviderTests()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);

        db.Languages.AddRange(
            new PublicLanguage { Id = 1, TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true },
            new PublicLanguage { Id = 2, TwoLetterCode = "en", IsDefault = false, IsActive = true, IsPublished = true });

        db.PageInfos.AddRange(
            // Parent page — content match only (title doesn't contain the term).
            NewPage(1, "kardiyoloji", "kardiyoloji", "Kardiyoloji Bölümü", languageId: 1, parentPageId: null,
                html: "<p>Kalp sağlığı hakkında bilgiler burada yer alır.</p>"),
            // Child page — content match only; category should resolve to the parent's title.
            NewPage(2, "sami-sokucu", "kardiyoloji/sami-sokucu", "Dr. Sami Sökücü", languageId: 1, parentPageId: 1,
                html: "<p>Sami Sökücü Kalp cerrahisi uzmanıdır.</p>"),
            // Title match — must rank above the two content-only matches above.
            NewPage(7, "kalp-uzmanlari", "kalp-uzmanlari", "Kalp Uzmanları", languageId: 1, parentPageId: null, html: null),
            // False positive: the term only appears inside a class attribute, never in
            // visible text, title or slug — must be excluded despite matching the raw-HTML
            // SQL prefilter.
            NewPage(3, "hakkimizda", "hakkimizda", "Hakkımızda", languageId: 1, parentPageId: null,
                html: "<div class=\"Kalp-kutu\">Ekibimizle tanışın.</div>"),
            // Wrong status — excluded regardless of match.
            NewPage(4, "kalp-klinigi", "kalp-klinigi", "Kalp Kliniği", languageId: 1, parentPageId: null,
                html: null, status: PublicPageStatus.Draft),
            // Wrong language — excluded when searching "tr".
            NewPage(5, "kalp-health-en", "en/kalp-health-en", "Kalp Health", languageId: 2, parentPageId: null, html: null),
            // Inactive — excluded regardless of match.
            NewPage(6, "kalp-inaktif", "kalp-inaktif", "Kalp İnaktif", languageId: 1, parentPageId: null,
                html: null, isActive: false),
            // The term exists only inside a component script — code, never read.
            NewPage(8, "betikli", "betikli", "Betikli Sayfa", languageId: 1, parentPageId: null,
                html: "<script>var Zeytin = 1;</script><p>Görünen metin.</p>"),
            // The term exists only inside a linked header template, which is the same
            // menu on every page and must not make every page a match.
            NewPage(9, "sablonlu", "sablonlu", "Şablonlu Sayfa", languageId: 1, parentPageId: null,
                html: "<div class=\"elevare-tpl-ref\" data-elevare-template-id=\"1\"><nav>Menüdekiler</nav></div><p>Gövde metni.</p>"),
            // Two blocks whose text only reads as a phrase once they are spaced apart.
            NewPage(10, "bloklu", "bloklu", "Bloklu Sayfa", languageId: 1, parentPageId: null,
                html: "<section><h2>Portakal</h2><p>Bahçesi burada.</p></section>"));

        db.SaveChanges();
    }

    private static PublicPage NewPage(
        int id, string slug, string fullSlug, string title, int languageId, int? parentPageId, string? html,
        PublicPageStatus status = PublicPageStatus.Published, bool isActive = true) => new()
    {
        Id = id,
        Slug = slug,
        FullSlug = fullSlug,
        LanguageId = languageId,
        ParentPageId = parentPageId,
        PageStatus = status,
        IsActive = isActive,
        SeoTitle = title,
        SeoMetaDescription = "",
        Content = html is null ? null : new PublicPageContent { Id = id, PageInfoId = id, GjsHtml = html }
    };

    /// <summary>
    /// Unwraps the provider's <c>Result</c>, asserting success first — every test in
    /// this class is about matching behaviour, so a failed search here means the test
    /// setup broke rather than the assertion under test.
    /// </summary>
    private async Task<List<SearchResultItemResponse>> SearchAsync(string term, string languageCode, int maxResults = 12)
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        SqlSearchProvider provider = new(db);

        Result<List<SearchResultItemResponse>> result =
            await provider.SearchAsync(term, languageCode, maxResults, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    [Fact]
    public async Task Matches_visible_page_content_not_just_title_and_slug()
    {
        List<SearchResultItemResponse> results = await SearchAsync("Kalp", "tr");

        results.Select(r => r.Title).ShouldContain("Kardiyoloji Bölümü");
        results.Select(r => r.Title).ShouldContain("Dr. Sami Sökücü");
    }

    [Fact]
    public async Task Excludes_pages_where_the_term_only_appears_inside_markup()
    {
        List<SearchResultItemResponse> results = await SearchAsync("Kalp", "tr");

        results.Select(r => r.Title).ShouldNotContain("Hakkımızda");
    }

    [Fact]
    public async Task Excludes_draft_inactive_and_wrong_language_pages()
    {
        List<SearchResultItemResponse> results = await SearchAsync("Kalp", "tr");

        results.Select(r => r.Title).ShouldNotContain("Kalp Kliniği");
        results.Select(r => r.Title).ShouldNotContain("Kalp İnaktif");
        results.Select(r => r.Title).ShouldNotContain("Kalp Health");
    }

    [Fact]
    public async Task Title_matches_are_ranked_above_content_only_matches()
    {
        List<SearchResultItemResponse> results = await SearchAsync("Kalp", "tr");

        results[0].Title.ShouldBe("Kalp Uzmanları");
    }

    [Fact]
    public async Task Category_resolves_to_the_immediate_parent_pages_title()
    {
        List<SearchResultItemResponse> results = await SearchAsync("Kalp", "tr");

        SearchResultItemResponse child = results.Single(r => r.Title == "Dr. Sami Sökücü");
        child.Category.ShouldBe("Kardiyoloji Bölümü");
    }

    [Fact]
    public async Task Top_level_page_has_no_category()
    {
        List<SearchResultItemResponse> results = await SearchAsync("Kalp", "tr");

        SearchResultItemResponse parent = results.Single(r => r.Title == "Kardiyoloji Bölümü");
        parent.Category.ShouldBeNull();
    }

    [Fact]
    public async Task Excerpt_is_built_around_the_matched_content()
    {
        List<SearchResultItemResponse> results = await SearchAsync("Kalp", "tr");

        SearchResultItemResponse parent = results.Single(r => r.Title == "Kardiyoloji Bölümü");
        parent.Excerpt.ShouldContain("Kalp sağlığı");
    }

    [Fact]
    public async Task Unknown_language_code_returns_empty()
    {
        List<SearchResultItemResponse> results = await SearchAsync("Kalp", "xx");

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Max_results_caps_the_returned_list()
    {
        List<SearchResultItemResponse> results = await SearchAsync("Kalp", "tr", maxResults: 1);

        results.Count.ShouldBe(1);
    }

    /// <summary>
    /// Searching is case-insensitive, in every position.
    /// <para>
    /// <b>What this test does and does not prove.</b> It runs against the EF in-memory
    /// provider, where the query is executed as ordinary .NET code — so it pins the
    /// INTENT and catches anyone reintroducing a case-sensitive comparison in C#, but
    /// it cannot reproduce the actual bug, which lived in how PostgreSQL executes
    /// LIKE. Verifying the real thing needs a Postgres-backed run.
    /// </para>
    /// <para>
    /// It was not, and the reason was easy to miss: the post-filter already used
    /// OrdinalIgnoreCase, so the code READ as case-insensitive — but the SQL
    /// prefilter above it used string.Contains, which becomes a case-SENSITIVE LIKE
    /// in PostgreSQL. Rows were thrown away before the insensitive check ever saw
    /// them, so "kalp" found nothing while "Kalp" worked.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("kalp")]      // all lower
    [InlineData("KALP")]      // all upper
    [InlineData("KaLp")]      // mixed
    public async Task Finds_the_same_pages_regardless_of_case(string term)
    {
        List<SearchResultItemResponse> results = await SearchAsync(term, "tr");

        results.ShouldNotBeEmpty();
        results.ShouldContain(r => r.Title == "Kalp Uzmanları");
    }

    /// <summary>
    /// A title match found through a differently-cased term still outranks
    /// content-only matches — the case fix must not disturb the ordering.
    /// </summary>
    [Fact]
    public async Task Case_insensitive_matches_keep_the_title_first_ordering()
    {
        List<SearchResultItemResponse> results = await SearchAsync("kalp", "tr");

        results[0].Title.ShouldBe("Kalp Uzmanları");
    }

    /// <summary>
    /// The class-attribute false positive stays excluded when the term is lower-cased:
    /// the visible-text check is what rejects it, and widening the prefilter must not
    /// let it through.
    /// </summary>
    [Fact]
    public async Task A_lower_cased_term_still_ignores_matches_that_are_only_in_markup()
    {
        List<SearchResultItemResponse> results = await SearchAsync("kalp", "tr");

        results.ShouldNotContain(r => r.Title == "Hakkımızda");
    }

    [Fact]
    public async Task Text_inside_scripts_is_not_searchable()
    {
        List<SearchResultItemResponse> results = await SearchAsync("Zeytin", "tr");

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Linked_header_and_footer_templates_are_not_searchable()
    {
        List<SearchResultItemResponse> results = await SearchAsync("Menüdekiler", "tr");

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Adjacent_blocks_are_read_as_separate_words()
    {
        List<SearchResultItemResponse> results = await SearchAsync("Portakal Bahçesi", "tr");

        SearchResultItemResponse page = results.ShouldHaveSingleItem();
        page.Title.ShouldBe("Bloklu Sayfa");
        page.Excerpt.ShouldBe("Portakal Bahçesi burada.");
    }
}
