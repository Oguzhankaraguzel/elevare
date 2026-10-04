namespace Domain.Entities.SiteCodeSnippets;

/// <summary>
/// "This site code does not run on this page." A plain many-to-many join row, the
/// same shape as <c>PageInfoTag</c>: added and removed outright, never restored on
/// its own, and gone by cascade when either side is permanently deleted — which is
/// what makes it a table rather than a list of ids in a column, where the id of a
/// purged snippet would linger with nothing to say it no longer means anything.
/// <para>
/// Exclusion, not inclusion, on purpose: a site code is site-wide by definition
/// (see <see cref="SiteCodeSnippet"/>), and the page is the exception. A page that
/// wants code of its own has the builder's custom-code block for that.
/// </para>
/// </summary>
public sealed class PageInfoSiteCodeExclusion
{
    public int PageInfoId { get; set; }
    public int SiteCodeSnippetId { get; set; }
}
