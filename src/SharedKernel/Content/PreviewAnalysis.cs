namespace SharedKernel.Content;

/// <summary>
/// The contract between the CMS's SEO analyzer and the public site's preview page.
/// The analyzer opens the page's signed preview in a hidden frame; the site renders
/// it through the real pipeline and, once its own scripts have run, posts the final
/// document back to the CMS — so the report judges what a visitor and a crawler get
/// (resolved links, filled-in image attributes, the linked templates' current
/// content) rather than the editor's canvas.
/// </summary>
/// <remarks>
/// The CMS's origin travels inside the signed preview token, not in the query
/// string: the site lets only that origin frame the page and only that origin
/// receive it, and a token is the one thing a third party cannot forge. A preview
/// link without it — the "Önizle" button's — is not frameable, as before.
/// </remarks>
public static class PreviewAnalysis
{
    /// <summary>The token claim carrying the origin allowed to frame and receive the preview.</summary>
    public const string OriginClaim = "elevare_ao";

    /// <summary>The message the preview posts to that origin.</summary>
    public const string SnapshotMessage = "elevare-seo-snapshot";

    /// <summary>
    /// <paramref name="value"/> reduced to "scheme://host[:port]" when it is an
    /// absolute http(s) URL, otherwise null — so a malformed or hostile value can
    /// never reach a response header or a postMessage target.
    /// </summary>
    public static string? NormalizeOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return null;
        return uri.GetLeftPart(UriPartial.Authority);
    }
}
