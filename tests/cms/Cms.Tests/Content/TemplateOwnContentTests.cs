using Application.Features.Commands.PageTemplates.Shared;
using Domain.Entities.PageTemplates;
using Shouldly;

namespace Cms.Tests.Content;

/// <summary>
/// Covers <see cref="TemplateOwnContent"/>: a copy template's content version only
/// moves when something a page's copy would be missing changed — not when the
/// linked templates inside it did, which pages receive live anyway.
/// </summary>
public sealed class TemplateOwnContentTests
{
    private static readonly DateTime Before = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PageTemplate Template(string html) =>
        new() { Name = "Makale Sayfası", GjsHtml = html, ContentChangedAt = Before };

    private const string Article = """
        <div class="elevare-tpl-ref" data-elevare-template-id="1"><header>MENU v1</header></div>
        <article><h1>Başlık</h1></article>
        """;

    [Fact]
    public void A_change_only_inside_a_linked_template_does_not_move_the_version()
    {
        PageTemplate template = Template(Article);

        TemplateOwnContent.Apply(template, Article.Replace("MENU v1", "MENU v2 <nav>yeni</nav>", StringComparison.Ordinal));

        template.ContentChangedAt.ShouldBe(Before);
    }

    [Fact]
    public void Re_saving_with_different_whitespace_does_not_move_it_either()
    {
        PageTemplate template = Template(Article);

        TemplateOwnContent.Apply(template, Article.Replace("\n", "\n    ", StringComparison.Ordinal));

        template.ContentChangedAt.ShouldBe(Before);
    }

    [Fact]
    public void A_change_to_the_templates_own_content_does()
    {
        PageTemplate template = Template(Article);

        TemplateOwnContent.Apply(template, Article.Replace("Başlık", "Yeni başlık", StringComparison.Ordinal));

        template.ContentChangedAt.ShouldNotBeNull();
        template.ContentChangedAt.Value.ShouldBeGreaterThan(Before);
    }
}
