namespace Domain.Entities.SiteCodeSnippets;

/// <summary>
/// Which known third-party tool a snippet came from.
/// <para>
/// A preset exists so the author can paste whatever the vendor's setup page handed
/// them — a bare measurement id or the whole copy-paste block — and still end up
/// with correct markup. The value is kept on the row afterwards so the list can say
/// "this is your Google Analytics tag" rather than showing a wall of script.
/// </para>
/// </summary>
public enum SiteCodePreset
{
    /// <summary>Hand-written snippet; validated against its <see cref="SiteCodeKind"/> and nothing more.</summary>
    Custom = 1,

    /// <summary>GA4. Accepts <c>G-XXXXXXX</c> or the full gtag.js block.</summary>
    GoogleAnalytics4,

    /// <summary>GTM. Accepts <c>GTM-XXXXXXX</c> or either half of the vendor snippet; always produces both halves.</summary>
    GoogleTagManager,

    /// <summary>Meta (Facebook) Pixel. Accepts a numeric pixel id or the full block.</summary>
    MetaPixel,

    /// <summary>Search Console ownership proof. Accepts the raw token or the whole <c>&lt;meta&gt;</c> tag.</summary>
    SearchConsoleVerification,

    /// <summary>Cookie-consent platform (CookieYes, Cookiebot, OneTrust, Iubenda…). Pinned to run first.</summary>
    CookieConsent
}
