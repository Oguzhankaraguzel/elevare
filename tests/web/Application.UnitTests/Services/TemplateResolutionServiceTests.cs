using Application.Services;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using Shouldly;
using SharedKernel.Concrete;

namespace Application.UnitTests.Services;

/// <summary>
/// Verifies that "linked template" marker blocks
/// (<c>&lt;div class="elevare-tpl-ref" data-elevare-template-id="N"&gt;</c>) are
/// replaced with the template's CURRENT content at render time — the mechanism that
/// makes editing a linked template propagate to every published page that uses it.
/// </summary>
public sealed class TemplateResolutionServiceTests
{
    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();

    public TemplateResolutionServiceTests()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        db.PageTemplates.AddRange(
            new PublicPageTemplate
            {
                Id = 5,
                Name = "Ana menü",
                Type = PublicPageTemplateType.Menu,
                IsLinked = true,
                GjsHtml = "<nav>GUNCEL MENU<button id=\"itheme\">tema</button></nav>"
                    + "<script>var items = document.querySelectorAll('#itheme');\n  for (var i = 0, len = items.length; i < len; i++) {\n    (function(){var root=this;root.addEventListener('click',function(){TOGGLE();});}.bind(items[i]))();\n  }</script>",
                GjsCss = ".nav{color:red}"
            },
            new PublicPageTemplate
            {
                Id = 6,
                Name = "Silinmiş şablon",
                Type = PublicPageTemplateType.Other,
                IsLinked = true,
                IsDeleted = true,
                GjsHtml = "<p>SILINMIS</p>"
            },
            // A menu holding a linked search box, saved with an old copy of it.
            new PublicPageTemplate
            {
                Id = 7, Name = "Menü", Type = PublicPageTemplateType.Menu, IsLinked = true,
                GjsHtml = """<header>MENU<div class="elevare-tpl-ref" data-elevare-template-id="8">ESKI ARAMA</div></header>""",
            },
            new PublicPageTemplate
            {
                Id = 8, Name = "Arama", Type = PublicPageTemplateType.Other, IsLinked = true,
                GjsHtml = "<form>GUNCEL ARAMA</form>", GjsCss = ".ara{}",
            },
            // Refers to itself — must not nest without end.
            new PublicPageTemplate
            {
                Id = 9, Name = "Döngü", Type = PublicPageTemplateType.Other, IsLinked = true,
                GjsHtml = """<p>DONGU</p><div class="elevare-tpl-ref" data-elevare-template-id="9"></div>""",
            });
        db.SaveChanges();
    }

    /// <summary>
    /// Unwraps the service's <c>Result</c>, asserting success first — these tests are
    /// about resolution behaviour, so a failure here means the fixture broke.
    /// </summary>
    private async Task<(string? Html, string Css)> ResolveAsync(string? html)
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        TemplateResolutionService service = new(db);

        Result<ResolvedTemplateContent> result = await service.ResolveAsync(html, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        return (result.Value.Html, result.Value.Css);
    }

    [Fact]
    public async Task Linked_marker_is_replaced_with_current_template_content()
    {
        (string? html, string css) = await ResolveAsync(
            """<div class="elevare-tpl-ref" data-elevare-template-id="5"><nav>ESKI MENU</nav></div>""");

        html.ShouldNotBeNull();
        html.ShouldContain("GUNCEL MENU");
        html.ShouldNotContain("ESKI MENU");
        css.ShouldContain(".nav{color:red}");
    }

    [Fact]
    public async Task Multiple_markers_for_the_same_template_are_all_refreshed()
    {
        (string? html, _) = await ResolveAsync(
            """
            <div class="elevare-tpl-ref" data-elevare-template-id="5"><nav>ESKI 1</nav></div>
            <p>ara icerik</p>
            <div class="elevare-tpl-ref" data-elevare-template-id="5"><nav>ESKI 2</nav></div>
            """);

        html.ShouldNotBeNull();
        html.ShouldNotContain("ESKI 1");
        html.ShouldNotContain("ESKI 2");
        html.ShouldContain("ara icerik");
    }

    [Fact]
    public async Task Marker_pointing_at_unknown_template_keeps_its_stored_content()
    {
        (string? html, _) = await ResolveAsync(
            """<div class="elevare-tpl-ref" data-elevare-template-id="999"><nav>ESKI</nav></div>""");

        html.ShouldNotBeNull();
        html.ShouldContain("ESKI");
    }

    [Fact]
    public async Task Marker_pointing_at_soft_deleted_template_keeps_its_stored_content()
    {
        (string? html, _) = await ResolveAsync(
            """<div class="elevare-tpl-ref" data-elevare-template-id="6"><p>KOPYA</p></div>""");

        html.ShouldNotBeNull();
        html.ShouldContain("KOPYA");
        html.ShouldNotContain("SILINMIS");
    }

    [Fact]
    public async Task Content_without_markers_passes_through_untouched()
    {
        (string? html, string css) = await ResolveAsync("<h2>Selam</h2><p>Metin</p>");

        html.ShouldNotBeNull();
        html.ShouldContain("<h2>Selam</h2>");
        html.ShouldContain("<p>Metin</p>");
        css.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task GrapeJs_body_wrapper_is_stripped_from_the_output()
    {
        // GrapeJS's editor.getHtml() often wraps exported content in a <body> tag;
        // emitting that inside the layout's own <body> would be invalid HTML.
        (string? html, _) = await ResolveAsync("<body><h2>Selam</h2></body>");

        html.ShouldNotBeNull();
        html.ShouldNotContain("<body");
        html.ShouldContain("<h2>Selam</h2>");
    }

    [Fact]
    public async Task Null_and_empty_inputs_are_returned_as_is()
    {
        (string? nullHtml, string nullCss) = await ResolveAsync(null);
        (string? emptyHtml, string emptyCss) = await ResolveAsync("");

        nullHtml.ShouldBeNull();
        nullCss.ShouldBe(string.Empty);
        emptyHtml.ShouldBe("");
        emptyCss.ShouldBe(string.Empty);
    }

    /// <summary>
    /// The wrapper itself does not survive into the published markup.
    /// <para>
    /// It is an editor construct — the thing you select and move in the page builder
    /// to manage a linked template. Once the template has been substituted in it has
    /// no job left, and leaving it behind put a bare
    /// <c>&lt;div class="elevare-tpl-ref" data-elevare-template-id="N"&gt;</c> in
    /// everyone's page source. Nothing on the public side reads it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_wrapper_element_is_removed_and_only_its_contents_remain()
    {
        (string? html, _) = await ResolveAsync(
            """<div class="elevare-tpl-ref" data-elevare-template-id="5"></div>""");

        html.ShouldNotBeNull();
        html.ShouldContain("GUNCEL MENU");
        html.ShouldNotContain("elevare-tpl-ref");
        html.ShouldNotContain("data-elevare-template-id");
    }

    /// <summary>
    /// Unlinked copies are never resolved — nothing replaces their content — but their
    /// wrapper is just as pointless once the page is rendered.
    /// </summary>
    [Fact]
    public async Task An_unlinked_snapshot_wrapper_is_removed_but_keeps_its_own_content()
    {
        (string? html, _) = await ResolveAsync(
            """<div class="elevare-tpl-snapshot" data-elevare-template-id="5"><header>KOPYA</header></div>""");

        html.ShouldNotBeNull();
        html.ShouldContain("KOPYA");
        html.ShouldNotContain("elevare-tpl-snapshot");
        // Its content is its own — resolution must not touch it.
        html.ShouldNotContain("GUNCEL MENU");
    }

    /// <summary>
    /// Unwrapping keeps the template's content where the wrapper stood, rather than
    /// moving it to the end — order is what a page layout is made of.
    /// </summary>
    [Fact]
    public async Task Unwrapping_preserves_document_order()
    {
        (string? html, _) = await ResolveAsync(
            """<p>ONCE</p><div class="elevare-tpl-ref" data-elevare-template-id="5"></div><p>SONRA</p>""");

        html.ShouldNotBeNull();
        int before = html.IndexOf("ONCE", StringComparison.Ordinal);
        int menu = html.IndexOf("GUNCEL MENU", StringComparison.Ordinal);
        int after = html.IndexOf("SONRA", StringComparison.Ordinal);
        before.ShouldBeLessThan(menu);
        menu.ShouldBeLessThan(after);
    }

    /// <summary>
    /// A page that embeds a linked template exports scripts for the template's
    /// components as well (its locked copy in the editor). Once the template's own
    /// script arrives with the substitution, the same button would be bound twice —
    /// on the live site the theme switch toggled dark→light→dark per click and did
    /// nothing visible. One binding per component id survives.
    /// </summary>
    [Fact]
    public async Task A_component_script_the_page_and_the_template_both_carry_is_emitted_once()
    {
        (string? html, _) = await ResolveAsync(
            """<div class="elevare-tpl-ref" data-elevare-template-id="5"></div><p>ICERIK</p>"""
            + "<script>var items=document.querySelectorAll('#itheme');for(var i=0,len=items.length;i<len;i++){(function(){var root=this;root.addEventListener('click',function(){TOGGLE();});}.bind(items[i]))();}\n"
            + "var items = document.querySelectorAll('#iown');\n  for (var i = 0, len = items.length; i < len; i++) {\n    (function(){OWN();}.bind(items[i]))();\n  }</script>");

        html.ShouldNotBeNull();
        // The builder exports these with spaces (`var items = document…`); the
        // minifier that removes them runs later, so the check must see this form.
        (html.Length - html.Replace("querySelectorAll('#itheme')", "").Length).ShouldBe("querySelectorAll('#itheme')".Length);
        // The page's own component keeps its script; only the duplicate went.
        html.ShouldContain("querySelectorAll('#iown')");
        html.ShouldContain("OWN();");
    }

    [Fact]
    public async Task A_linked_template_inside_a_linked_template_gets_its_current_content_too()
    {
        (string? html, string css) = await ResolveAsync("""<div class="elevare-tpl-ref" data-elevare-template-id="7"></div>""");

        html.ShouldBe("<header>MENU<form>GUNCEL ARAMA</form></header>");
        css.ShouldContain(".ara{}");
    }

    [Fact]
    public async Task A_template_inside_itself_is_not_nested_again()
    {
        (string? html, _) = await ResolveAsync("""<div class="elevare-tpl-ref" data-elevare-template-id="9"></div>""");

        html.ShouldNotBeNull();
        (html.Length - html.Replace("DONGU", "", StringComparison.Ordinal).Length).ShouldBe("DONGU".Length);
    }
}
