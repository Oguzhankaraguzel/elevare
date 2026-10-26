namespace SharedKernel.Content;

/// <summary>
/// Which copy of a page a signed preview link shows. The page editor's own links
/// (the Preview button, the SEO analyzer) show its unsaved canvas, kept in the
/// draft columns; every other link (a reviewer's, from the approvals screen) shows
/// what was saved — the content staged for approval when there is any, the live
/// content otherwise.
/// </summary>
/// <remarks>
/// The choice travels inside the signed token, like the analyzer's origin (see
/// <see cref="PreviewAnalysis"/>), so a link cannot be edited to show something
/// its signer did not mean it to.
/// </remarks>
public static class PreviewDraft
{
    /// <summary>The token claim present, as "true", on a link that shows the editor's draft.</summary>
    public const string Claim = "elevare_draft";
}
