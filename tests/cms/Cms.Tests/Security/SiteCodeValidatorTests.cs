using Application.Security;
using Domain.Entities.SiteCodeSnippets;
using Shouldly;

namespace Cms.Tests.Security;

/// <summary>
/// Pins what the Site Codes screen will and will not write into every public page.
/// The snippets below are the real shapes vendors hand out, plus the mistakes people
/// actually make when pasting them — an unclosed tag, a whole saved page, or content
/// that does not match the type they picked.
/// </summary>
public sealed class SiteCodeValidatorTests
{
    [Theory]
    [InlineData("""<script async src="https://www.googletagmanager.com/gtag/js?id=G-X"></script>""")]
    [InlineData("""<script>window.dataLayer = window.dataLayer || []; if (a < b) { f(); }</script>""")]
    [InlineData("""<script>var s = '</div>';</script>""")]
    public void Well_formed_scripts_are_accepted(string content)
        => SiteCodeValidator.Validate(SiteCodeKind.Script, content).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("""<script>console.log("half");""")]
    [InlineData("""<div><span>unclosed""")]
    [InlineData("""<noscript><iframe src="x"></noscript>""")]
    public void Unclosed_tags_are_rejected(string content)
        => SiteCodeValidator.Validate(SiteCodeKind.RawHtml, content).IsValid.ShouldBeFalse();

    [Theory]
    [InlineData("""<html><head><title>x</title></head><body>y</body></html>""")]
    [InlineData("""<body><script>x()</script></body>""")]
    public void A_whole_pasted_document_is_rejected(string content)
        => SiteCodeValidator.Validate(SiteCodeKind.RawHtml, content).IsValid.ShouldBeFalse();

    [Fact]
    public void Void_elements_do_not_need_closing_tags()
        => SiteCodeValidator
            .Validate(SiteCodeKind.MetaTag, """<meta name="google-site-verification" content="abc">""")
            .IsValid.ShouldBeTrue();

    [Fact]
    public void Comments_are_not_counted_as_markup()
        => SiteCodeValidator
            .Validate(SiteCodeKind.RawHtml, """<!-- <div> unbalanced inside a comment --><span>ok</span>""")
            .IsValid.ShouldBeTrue();

    [Fact]
    public void Content_that_does_not_match_the_chosen_kind_is_rejected()
        => SiteCodeValidator
            .Validate(SiteCodeKind.Script, """<style>.a{color:red}</style>""")
            .IsValid.ShouldBeFalse();

    [Fact]
    public void A_second_meta_tag_is_rejected_so_each_can_be_switched_off_alone()
        => SiteCodeValidator
            .Validate(SiteCodeKind.MetaTag, """<meta name="a" content="1"><meta name="b" content="2">""")
            .IsValid.ShouldBeFalse();

    [Fact]
    public void Empty_content_is_rejected()
        => SiteCodeValidator.Validate(SiteCodeKind.Script, "   ").IsValid.ShouldBeFalse();

    [Fact]
    public void A_pasted_library_is_rejected_on_size()
    {
        string oversized = $"<script>{new string('x', SiteCodeValidator.MaxContentLength)}</script>";

        SiteCodeValidator.Validate(SiteCodeKind.Script, oversized).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Document_write_is_warned_about_but_still_saved()
    {
        SiteCodeValidationResult result =
            SiteCodeValidator.Validate(SiteCodeKind.Script, """<script>document.write("<b>hi</b>")</script>""");

        result.IsValid.ShouldBeTrue();
        result.Warnings.ShouldContain("DocumentWrite");
    }
}
