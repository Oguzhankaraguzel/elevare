using Application.Features.Queries.Workflows.GetApprovalDiff;
using Shouldly;

namespace Cms.Tests.Workflows;

public class HtmlTextDiffTests
{
    private static string Text(List<ApprovalDiffLine> lines, ApprovalDiffKind kind) =>
        string.Join("|", lines.Where(l => l.Kind == kind).Select(l => l.Text));

    [Fact]
    public void Identical_content_reports_no_changes()
    {
        const string html = "<div><h1>Hizmetlerimiz</h1><p>Uzman kadromuz.</p></div>";

        List<ApprovalDiffLine> lines = HtmlTextDiff.Compare(html, html);

        lines.ShouldAllBe(l => l.Kind == ApprovalDiffKind.Unchanged);
    }

    [Fact]
    public void Changed_sentence_is_reported_as_one_removal_and_one_addition()
    {
        List<ApprovalDiffLine> lines = HtmlTextDiff.Compare(
            "<p>Eski cümle.</p><p>Aynı kalan.</p>",
            "<p>Yeni cümle.</p><p>Aynı kalan.</p>");

        Text(lines, ApprovalDiffKind.Removed).ShouldBe("Eski cümle.");
        Text(lines, ApprovalDiffKind.Added).ShouldBe("Yeni cümle.");
        Text(lines, ApprovalDiffKind.Unchanged).ShouldBe("Aynı kalan.");
    }

    [Fact]
    public void Markup_only_changes_produce_no_diff()
    {
        // The whole point of diffing text instead of markup: GrapesJS rewrites ids
        // and classes on nearly every save, and none of that is a content change.
        List<ApprovalDiffLine> lines = HtmlTextDiff.Compare(
            "<div id=\"i3kj\" class=\"row\"><p class=\"a\">Merhaba</p></div>",
            "<section id=\"zz91\" class=\"grid gap\"><p class=\"b c\">Merhaba</p></section>");

        lines.ShouldHaveSingleItem().Kind.ShouldBe(ApprovalDiffKind.Unchanged);
    }

    [Fact]
    public void Script_and_style_bodies_are_not_treated_as_content()
    {
        List<ApprovalDiffLine> lines = HtmlTextDiff.Compare(
            "<p>Metin</p><script>var a = 1;</script><style>.x{color:red}</style>",
            "<p>Metin</p><script>var a = 2;</script><style>.x{color:blue}</style>");

        lines.ShouldAllBe(l => l.Kind == ApprovalDiffKind.Unchanged);
    }

    [Fact]
    public void Whitespace_reflow_is_not_a_change()
    {
        List<ApprovalDiffLine> lines = HtmlTextDiff.Compare(
            "<p>Bir   iki\n   üç</p>",
            "<p>\n  Bir iki üç\n</p>");

        lines.ShouldHaveSingleItem().Kind.ShouldBe(ApprovalDiffKind.Unchanged);
    }

    [Fact]
    public void First_publish_reports_every_line_as_added()
    {
        List<ApprovalDiffLine> lines = HtmlTextDiff.Compare(null, "<h1>Yeni</h1><p>Sayfa</p>");

        lines.Count.ShouldBe(2);
        lines.ShouldAllBe(l => l.Kind == ApprovalDiffKind.Added);
    }

    [Fact]
    public void Inserted_paragraph_keeps_the_surrounding_lines_unchanged()
    {
        List<ApprovalDiffLine> lines = HtmlTextDiff.Compare(
            "<p>A</p><p>C</p>",
            "<p>A</p><p>B</p><p>C</p>");

        Text(lines, ApprovalDiffKind.Added).ShouldBe("B");
        Text(lines, ApprovalDiffKind.Removed).ShouldBeEmpty();
        Text(lines, ApprovalDiffKind.Unchanged).ShouldBe("A|C");
    }
}
