using Domain.Entities.Abstractions;
using Domain.Entities.PageInfos;

namespace Domain.Entities.PageContents;

/// <summary>
/// GrapeJS-produced body content for a builder <see cref="PageInfo"/> (1:1).
/// The visual editor exports rendered <see cref="GjsHtml"/> + <see cref="GjsCss"/>
/// (served on the public site) and a project blob <see cref="GjsData"/> that is
/// reloaded to restore the editor's exact state for further editing.
/// </summary>
public class PageContent : BaseEntity
{
    /// <summary>Owning page (1:1).</summary>
    public int PageInfoId { get; set; }

    /// <summary>Rendered HTML exported by GrapeJS.</summary>
    public string? GjsHtml { get; set; }

    /// <summary>Rendered CSS exported by GrapeJS.</summary>
    public string? GjsCss { get; set; }

    /// <summary>Full GrapeJS project JSON (components + styles) used to restore the editor.</summary>
    public string? GjsData { get; set; }

    /// <summary>
    /// Staged HTML for the "preview without publishing" flow — set by
    /// <c>SavePreviewContentCommand</c>, read only by preview links, and cleared on every
    /// real save so a stale snapshot never lingers past a genuine publish.
    /// </summary>
    public string? PreviewGjsHtml { get; set; }

    /// <summary>Staged CSS counterpart to <see cref="PreviewGjsHtml"/>.</summary>
    public string? PreviewGjsCss { get; set; }

    public virtual PageInfo PageInfo { get; set; } = null!;
}
