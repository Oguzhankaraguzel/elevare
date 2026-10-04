using Application.Services;
using SharedKernel.Content;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Covers <see cref="CodeHighlighter"/> and <see cref="ContentEnhancer"/>: code
/// coloured on the server, and an article's reading time from its own words.
/// </summary>
public sealed class ContentEnhancerTests
{
    [Fact]
    public void Csharp_keywords_strings_comments_numbers_types_and_calls_are_marked()
    {
        string html = CodeHighlighter.Highlight("// sum\npublic int Add(int a) => a + 1; var s = \"x<y\"; List<int> l;", "csharp");

        html.ShouldContain("<span class=\"tk-c\">// sum</span>");
        html.ShouldContain("<span class=\"tk-k\">public</span>");
        html.ShouldContain("<span class=\"tk-f\">Add</span>");
        html.ShouldContain("<span class=\"tk-n\">1</span>");
        html.ShouldContain("<span class=\"tk-s\">&quot;x&lt;y&quot;</span>");
        html.ShouldContain("<span class=\"tk-t\">List</span>");
    }

    [Fact]
    public void Nothing_in_the_code_can_become_markup()
    {
        string html = CodeHighlighter.Highlight("<script>alert(1)</script>", "plaintext");

        html.ShouldBe("&lt;script&gt;alert(1)&lt;/script&gt;");
    }

    [Theory]
    [InlineData("C#", "csharp")]
    [InlineData("js", "javascript")]
    [InlineData("SQL", "sql")]
    [InlineData("klingon", "plaintext")]
    [InlineData(null, "plaintext")]
    public void Language_names_and_aliases_are_understood(string? given, string expected)
    {
        CodeHighlighter.Normalize(given).ShouldBe(expected);
    }

    [Fact]
    public void Markup_tags_attributes_and_values_are_marked()
    {
        string html = CodeHighlighter.Highlight("<a href=\"/x\">y</a>", "html");

        html.ShouldContain("<span class=\"tk-g\">&lt;a</span>");
        html.ShouldContain("<span class=\"tk-p\">href</span>");
        html.ShouldContain("<span class=\"tk-s\">&quot;/x&quot;</span>");
    }

    [Fact]
    public void Json_keys_are_told_from_values()
    {
        string html = CodeHighlighter.Highlight("{\"name\": \"Ada\"}", "json");

        html.ShouldContain("<span class=\"tk-p\">&quot;name&quot;</span>");
        html.ShouldContain("<span class=\"tk-s\">&quot;Ada&quot;</span>");
    }

    [Fact]
    public async Task A_code_block_is_coloured_split_into_lines_and_the_theme_is_added_once()
    {
        string block = "<div data-elevare-code data-language=\"csharp\"><pre><code>/* a\nb */\nint x;</code></pre></div>";

        string? html = await ContentEnhancer.EnhanceAsync(block + block, "tr", CancellationToken.None);

        html.ShouldNotBeNull();
        (html.Length - html.Replace("data-elevare-code-theme", "", StringComparison.Ordinal).Length).ShouldBe("data-elevare-code-theme".Length);
        // A comment spanning a line break is closed and reopened, so every line is whole.
        html.ShouldContain("<span class=\"el-code-line\"><span class=\"tk-c\">/* a</span></span>");
        html.ShouldContain("<span class=\"el-code-line\"><span class=\"tk-c\">b */</span></span>");
        html.ShouldContain("class=\"language-csharp\"");
    }

    [Fact]
    public async Task Reading_time_counts_the_articles_own_words_in_the_pages_language()
    {
        string words = string.Join(' ', Enumerable.Repeat("kelime", 1000));
        string article = $"<article data-elevare-article><p><span data-elevare-article-reading-time>1 dk</span></p><div data-elevare-article-body><p>{words}</p></div></article>";

        (await ContentEnhancer.EnhanceAsync(article, "tr", CancellationToken.None) ?? "").ShouldContain(">5 dk okuma<");
        (await ContentEnhancer.EnhanceAsync(article, "en", CancellationToken.None) ?? "").ShouldContain(">5 min read<");
    }

    [Fact]
    public async Task A_page_without_either_is_returned_as_it_was()
    {
        const string html = "<p>Hiçbir şey</p>";

        (await ContentEnhancer.EnhanceAsync(html, "tr", CancellationToken.None)).ShouldBe(html);
    }

    private const string SearchBox = """
        <div class="elevare-search-box" data-elevare-block="elevare-search-box">
          <form><input name="q" placeholder="Ara..." aria-label="Sitede ara"></form>
          <div data-elevare-search-empty-template>Sonuç bulunamadı.</div>
        </div>
        <button data-elevare-code-copy data-copied-text="Kopyalandı" aria-label="Kodu kopyala">Kopyala</button>
        """;

    [Fact]
    public async Task The_builders_own_wording_follows_the_pages_language()
    {
        string html = await ContentEnhancer.EnhanceAsync(SearchBox, "en", CancellationToken.None) ?? "";

        html.ShouldContain("placeholder=\"Search...\"");
        html.ShouldContain("aria-label=\"Search the site\"");
        html.ShouldContain(">No results found.<");
        html.ShouldContain("data-copied-text=\"Copied\"");
        html.ShouldContain(">Copy<");
    }

    [Fact]
    public async Task English_defaults_on_a_turkish_page_become_turkish()
    {
        string english = SearchBox.Replace("Ara...", "Search...", StringComparison.Ordinal)
            .Replace("Sonuç bulunamadı.", "No results found.", StringComparison.Ordinal);

        string html = await ContentEnhancer.EnhanceAsync(english, "tr", CancellationToken.None) ?? "";

        html.ShouldContain("placeholder=\"Ara...\"");
        html.ShouldContain(">Sonuç bulunamadı.<");
    }

    [Fact]
    public async Task A_newsletter_block_placed_from_a_turkish_editor_reads_english_on_an_english_page()
    {
        const string newsletter = """
            <section data-elevare-block="elevare-newsletter"><h2 class="el-nl-title">Gelişmelerden Haberdar Olun</h2>
            <form><input class="el-nl-input" placeholder="E-posta adresiniz"><button class="el-nl-btn">Abone Ol</button>
            <label><input type="checkbox" name="consent"><span class="el-nl-consent-text">Bülten e-postalarını almayı kabul ediyorum; istediğim zaman ayrılabilirim.</span></label></form></section>
            """;

        string html = await ContentEnhancer.EnhanceAsync(newsletter, "en", CancellationToken.None) ?? "";

        html.ShouldContain(">Stay in the Loop<");
        html.ShouldContain("placeholder=\"Your email address\"");
        html.ShouldContain(">Subscribe<");
        html.ShouldContain(">I agree to receive the newsletter and can unsubscribe at any time.<");
    }

    [Fact]
    public async Task Wording_the_author_wrote_is_left_alone()
    {
        string own = SearchBox.Replace("Sonuç bulunamadı.", "Aradığın yazı yok, başka bir şey dene.", StringComparison.Ordinal)
            .Replace("Ara...", "Makale ara", StringComparison.Ordinal);

        string html = await ContentEnhancer.EnhanceAsync(own, "en", CancellationToken.None) ?? "";

        html.ShouldContain(">Aradığın yazı yok, başka bir şey dene.<");
        html.ShouldContain("placeholder=\"Makale ara\"");
    }
}
