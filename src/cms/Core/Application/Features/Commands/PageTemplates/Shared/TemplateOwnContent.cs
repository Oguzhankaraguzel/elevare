using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Domain.Entities.PageTemplates;

namespace Application.Features.Commands.PageTemplates.Shared;

/// <summary>
/// Moves <see cref="PageTemplate.ContentChangedAt"/> only when what a page would
/// copy out of the template actually changed.
/// <para>
/// A copy template usually carries linked ones (a menu, a footer). Their markup is
/// saved inside it too, so re-saving the template after the menu changed alters its
/// HTML without changing anything a page's copy is missing — the page receives the
/// menu live. Comparing the HTML with every linked template's insides emptied tells
/// the two apart. Styles are left out on purpose: the template's stylesheet also
/// carries its linked templates' rules, so it changes with them.
/// </para>
/// </summary>
internal static partial class TemplateOwnContent
{
    private const string LinkedClass = "elevare-tpl-ref";

    /// <summary>Stamps the template when <paramref name="newHtml"/> differs from what it holds.</summary>
    public static void Apply(PageTemplate template, string? newHtml)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (!string.Equals(Fingerprint(template.GjsHtml), Fingerprint(newHtml), StringComparison.Ordinal))
            template.ContentChangedAt = DateTime.UtcNow;
    }

    /// <summary>The template's HTML with linked templates' insides removed and whitespace collapsed.</summary>
    public static string Fingerprint(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;
        try
        {
            using AngleSharp.Html.Dom.IHtmlDocument document = new HtmlParser().ParseDocument(html);
            foreach (AngleSharp.Dom.IElement linked in document.QuerySelectorAll("." + LinkedClass))
                linked.InnerHtml = string.Empty;
            return Whitespace().Replace(document.Body?.InnerHtml ?? html, " ").Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Whitespace().Replace(html, " ").Trim();
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
