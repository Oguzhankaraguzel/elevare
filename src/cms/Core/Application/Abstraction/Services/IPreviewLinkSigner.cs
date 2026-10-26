using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Signs the short-lived preview links the CMS hands out for unpublished pages.
/// The public site verifies them with the same secret (see its
/// <c>PreviewTokenService</c>) — the two MUST share <c>Preview:SigningKey</c>.
/// <para>
/// Returns a <see cref="Result"/> because the signing key is deploy-time
/// configuration that can simply be absent. That used to throw, which turned a
/// missing setting into an unhandled 500 on the editor's screen; as a failure it
/// becomes a message that names the setting.
/// </para>
/// </summary>
public interface IPreviewLinkSigner
{
    /// <param name="analysisOrigin">The CMS origin allowed to frame the preview and
    /// receive its rendered document (see <c>PreviewAnalysis</c>); null for an
    /// ordinary preview link, which stays unframeable.</param>
    /// <param name="showsDraft">True for the page editor's own links, which show its
    /// unsaved draft (see <c>PreviewDraft</c>); false shows what was saved.</param>
    Result<string> Sign(int pageId, DateTimeOffset expiry, string? analysisOrigin = null, bool showsDraft = false);
}
