namespace Application.Features.Queries.SiteSettings.GetSiteIdentity;

/// <param name="SiteName">General.SiteName, or null when empty.</param>
/// <param name="SiteUrl">Advanced.PublicSiteBaseUrl, only when it is an absolute http(s) URL.</param>
/// <param name="Host">The host of <paramref name="SiteUrl"/> as people read it ("www.example.com").</param>
/// <param name="FaviconUrl">Appearance.FaviconUrl, or null when empty.</param>
// Strings, not Uri: both go straight into href/src attributes, and FaviconUrl is a
// site setting that may be relative ("/uploads/…"), which Uri cannot hold as such.
#pragma warning disable CA1054
public sealed record SiteIdentityResponse(string? SiteName, string? SiteUrl, string? Host, string? FaviconUrl)
{
    /// <summary>
    /// Whether the site's own address is set. Until it is, the panel keeps showing
    /// Elevare's name and mark — the empty setting is already reported by the
    /// site-health warning, and a half-filled identity would only look broken.
    /// </summary>
    public bool IsConfigured => Host is not null;
}
#pragma warning restore CA1054
