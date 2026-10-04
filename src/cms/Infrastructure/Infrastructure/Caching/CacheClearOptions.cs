namespace Infrastructure.Caching;

/// <summary>
/// Bound from <c>appsettings.json → Cache</c>. <see cref="ClearSecret"/> must match
/// the Web app's own <c>Cache:ClearSecret</c> — it's how <c>/api/cache/clear</c>
/// tells the CMS apart from an arbitrary internet caller.
/// </summary>
public sealed class CacheClearOptions
{
    public const string SectionName = "Cache";

    public string ClearSecret { get; init; } = "";

    /// <summary>
    /// Where to reach the Web app for cache administration, when that is not the
    /// same address visitors use.
    /// <para>
    /// The public address is a poor choice for this call and frequently a broken
    /// one: it sends a request from the server back out to the internet and in
    /// again through DNS, the reverse proxy and whatever CDN or bot protection sits
    /// in front of the site — any of which can refuse a request that did not come
    /// from a browser. Under Docker Compose the two apps are on the same network
    /// and can simply talk directly, which is what this is for
    /// (<c>http://web:8080</c>).
    /// </para>
    /// <para>
    /// Empty falls back to the <c>Advanced.PublicSiteBaseUrl</c> site setting, so a
    /// single-host or non-container deployment keeps working with no configuration.
    /// </para>
    /// </summary>
    public string InternalWebBaseUrl { get; init; } = "";
}
