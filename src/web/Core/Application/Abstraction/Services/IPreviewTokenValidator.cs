using System.Diagnostics.CodeAnalysis;

namespace Application.Abstraction.Services;

/// <summary>
/// Validates the signed preview links the CMS hands out for unpublished pages
/// (see the CMS's <c>IPreviewLinkSigner</c> — both sides read the same
/// <c>Preview:SigningKey</c> secret and use the same JWT library, so there is no
/// hand-written encode/decode logic on either side to keep in sync).
/// </summary>
public interface IPreviewTokenValidator
{
    /// <summary>
    /// <c>true</c> when <paramref name="token"/> carries a valid signature and has
    /// not expired, with <paramref name="pageId"/> set to the page it authorizes.
    /// <c>false</c> for anything else (missing, malformed, expired, tampered, or
    /// signed with a different key) — <paramref name="pageId"/> is <c>0</c> in
    /// every one of those cases, deliberately not distinguished, so a caller can't
    /// be tempted to treat any rejection as partially trustworthy.
    /// </summary>
    bool TryValidate(string? token, out int pageId);

    /// <summary>
    /// As <see cref="TryValidate(string?, out int)"/>, with everything else the
    /// token decides: see <see cref="PreviewGrant"/>. <paramref name="grant"/> is
    /// null whenever the token is rejected.
    /// </summary>
    bool TryValidate(string? token, [NotNullWhen(true)] out PreviewGrant? grant);
}

/// <summary>What a valid preview token allows.</summary>
/// <param name="PageId">The page it previews.</param>
/// <param name="ShowsDraft">True for the CMS editor's own links, which show its
/// unsaved draft; false shows what was saved (see <c>PreviewDraft</c>).</param>
/// <param name="AnalysisOrigin">The CMS origin allowed to frame the preview and
/// receive its rendered document (see <c>PreviewAnalysis</c>); null for an ordinary
/// preview link.</param>
public sealed record PreviewGrant(int PageId, bool ShowsDraft, string? AnalysisOrigin);
