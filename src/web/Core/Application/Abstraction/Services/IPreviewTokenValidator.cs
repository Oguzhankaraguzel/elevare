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
}
