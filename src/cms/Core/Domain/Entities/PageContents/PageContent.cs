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
    /// HTML saved behind an approval workflow, waiting for the chain to clear — what
    /// <c>DecideApprovalCommand</c> promotes to live, and what a reviewer's preview
    /// link shows. Only a real save writes it; cleared when it is promoted or
    /// superseded by a direct publish.
    /// </summary>
    public string? PreviewGjsHtml { get; set; }

    /// <summary>Staged CSS counterpart to <see cref="PreviewGjsHtml"/>.</summary>
    public string? PreviewGjsCss { get; set; }

    /// <summary>
    /// The editor's unsaved canvas, written for its own preview links (the Preview
    /// button and the SEO analyzer) by <c>SavePreviewContentCommand</c>. A scratch
    /// copy: nothing publishes it, and every real save clears it. Kept apart from
    /// <see cref="PreviewGjsHtml"/> so previewing never replaces content awaiting
    /// approval or makes a page look as if it had an update pending.
    /// </summary>
    public string? DraftGjsHtml { get; set; }

    /// <summary>CSS counterpart to <see cref="DraftGjsHtml"/>.</summary>
    public string? DraftGjsCss { get; set; }

    public virtual PageInfo PageInfo { get; set; } = null!;
}
