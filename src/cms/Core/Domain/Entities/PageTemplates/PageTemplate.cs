using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Entities.Abstractions;
using Domain.Entities.Languages;

namespace Domain.Entities.PageTemplates;

/// <summary>
/// A reusable block of GrapeJS content (a menu, a full page layout, a WhatsApp button,
/// etc.) that editors can drop into any page.
/// <para>
/// <see cref="IsLinked"/> controls how insertions behave: when <c>true</c>, pages
/// that use this template keep a live reference and pick up future edits to it
/// automatically (resolved whenever the page is opened or rendered); when
/// <c>false</c>, inserting the template copies its current content and the two
/// become independent from that point on.
/// </para>
/// </summary>
public class PageTemplate : BaseEntity
{
    [MaxLength(200)]
    public required string Name { get; set; }

    public PageTemplateType Type { get; set; } = PageTemplateType.Other;

    /// <summary>
    /// The language this template's text is written in — null means it doesn't
    /// carry any (an icons-only social bar, for instance) and applies regardless
    /// of the page's own language. A header or footer with real copy needs one
    /// genuine template per language; this is not a fallback/default value, since
    /// a wrong guess here would silently show the wrong language's words.
    /// </summary>
    public int? LanguageId { get; set; }

    [ForeignKey(nameof(LanguageId))]
    public virtual Language? Language { get; set; }

    /// <summary>When true, pages referencing this template are refreshed with its latest content.</summary>
    public bool IsLinked { get; set; }

    /// <summary>Rendered HTML exported by GrapeJS.</summary>
    public string? GjsHtml { get; set; }

    /// <summary>Rendered CSS exported by GrapeJS.</summary>
    public string? GjsCss { get; set; }

    /// <summary>Full GrapeJS project JSON (components + styles) used to restore the editor.</summary>
    public string? GjsData { get; set; }

    /// <summary>
    /// Staged HTML pending workflow approval — set instead of <see cref="GjsHtml"/>
    /// while an active <see cref="Domain.Entities.Workflows.WorkflowDefinition"/> gates
    /// <see cref="Domain.Entities.Workflows.WorkflowContentType.PageTemplate"/>, and
    /// promoted to <see cref="GjsHtml"/> once the chain is fully approved.
    /// </summary>
    /// <summary>
    /// When the template's OWN content last changed — not counting the linked
    /// templates inside it, which every page already receives live. This, not
    /// <c>UpdateDate</c>, is the version a page's copy is compared against: a copy
    /// is only out of date when there is something in the template it did not get.
    /// Null for a template whose content has not been saved since this was added.
    /// </summary>
    public DateTime? ContentChangedAt { get; set; }

    public string? PreviewGjsHtml { get; set; }

    /// <summary>Staged CSS counterpart to <see cref="PreviewGjsHtml"/>.</summary>
    public string? PreviewGjsCss { get; set; }
}
