using Application.Services;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Covers <see cref="CssRuleDeduplicator"/> — the merge that joins a page's own CSS
/// with its linked templates' CSS without shipping the same rule twice.
/// </summary>
public sealed class CssRuleDeduplicatorTests
{
    [Fact]
    public void A_rule_present_in_both_sheets_is_emitted_once()
    {
        string merged = CssRuleDeduplicator.Merge(
            ".menu{color:red}.page{color:blue}",
            ".menu{color:red}");

        merged.ShouldBe(".page{color:blue}.menu{color:red}");
    }

    [Fact]
    public void The_last_copy_is_the_one_kept()
    {
        // The point of keeping the last copy rather than the first: .a and .b have
        // equal specificity, so an element carrying both classes takes whichever
        // came last. Keeping the first copy would flip it from red to blue.
        string merged = CssRuleDeduplicator.Merge(".a{color:red}.b{color:blue}.a{color:red}", null);

        merged.ShouldBe(".b{color:blue}.a{color:red}");
    }

    [Fact]
    public void Rules_that_only_share_a_selector_are_both_kept()
    {
        // Same selector, different declarations — this is the cascade doing its job,
        // not redundancy.
        string merged = CssRuleDeduplicator.Merge("body{margin:0}body{color:red}", null);

        merged.ShouldBe("body{margin:0}body{color:red}");
    }

    [Fact]
    public void A_media_block_is_compared_whole()
    {
        string merged = CssRuleDeduplicator.Merge(
            "@media (max-width:640px){.a{display:none}}",
            "@media (max-width:640px){.a{display:none}}");

        merged.ShouldBe("@media (max-width:640px){.a{display:none}}");
    }

    [Fact]
    public void Media_blocks_with_different_contents_both_survive()
    {
        string merged = CssRuleDeduplicator.Merge(
            "@media (max-width:640px){.a{display:none}}",
            "@media (max-width:640px){.b{display:none}}");

        merged.ShouldBe("@media (max-width:640px){.a{display:none}}@media (max-width:640px){.b{display:none}}");
    }

    [Fact]
    public void Indentation_and_line_breaks_do_not_hide_a_duplicate()
    {
        // Both sheets are minified in practice, so this only has to survive the
        // newline and indent an editor may put between rules — not a full
        // re-formatting. See the note on CssRuleDeduplicator.Merge.
        string merged = CssRuleDeduplicator.Merge(
            "\n    .a{color:red}\n",
            ".a{color:red}");

        // Trimmed: the kept copy keeps whatever inert whitespace preceded it in the
        // source, which the browser ignores.
        merged.Trim().ShouldBe(".a{color:red}");
    }

    [Fact]
    public void Whitespace_inside_a_quoted_value_is_significant()
    {
        // "a  b" and "a b" render differently, so these are two rules, not one.
        string merged = CssRuleDeduplicator.Merge(
            ".a::after{content:\"a  b\"}",
            ".a::after{content:\"a b\"}");

        merged.ShouldBe(".a::after{content:\"a  b\"}.a::after{content:\"a b\"}");
    }

    [Fact]
    public void Trailing_text_after_the_last_brace_is_preserved()
    {
        string merged = CssRuleDeduplicator.Merge(".a{color:red}", "/* yorum */");

        merged.ShouldBe(".a{color:red}/* yorum */");
    }

    [Fact]
    public void A_stray_closing_brace_does_not_swallow_the_rules_after_it()
    {
        string merged = CssRuleDeduplicator.Merge("}.a{color:red}.b{color:blue}", null);

        merged.ShouldContain(".a{color:red}");
        merged.ShouldContain(".b{color:blue}");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Empty_input_yields_empty_output(string? pageCss, string? templateCss)
        => CssRuleDeduplicator.Merge(pageCss, templateCss).ShouldBeEmpty();

    [Fact]
    public void Either_sheet_alone_is_returned_intact()
    {
        CssRuleDeduplicator.Merge(".a{color:red}", null).ShouldBe(".a{color:red}");
        CssRuleDeduplicator.Merge(null, ".b{color:blue}").ShouldBe(".b{color:blue}");
    }
}
