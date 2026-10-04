namespace WebMvc.RateLimiting;

/// <summary>
/// Names of the rate-limiting policies applied to the public site's write/query
/// endpoints. Page rendering itself is deliberately NOT limited — a crawler or a
/// traffic spike must not be turned into 429s for real visitors; the output cache
/// and the application cache are what protect those paths.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Form submissions. Tightest budget: this endpoint writes customer data and is
    /// the obvious spam target. Captcha (when configured) is the first line of
    /// defence, this is the second.
    /// </summary>
    public const string FormSubmit = "form-submit";

    /// <summary>
    /// Live search. Generous enough for debounced typing (one call per ~300ms of
    /// typing) but bounded, since each call runs a DB query plus HTML-to-text parsing.
    /// </summary>
    public const string Search = "search";

    /// <summary>
    /// Anonymous telemetry (page views, durations, clicks). Highest budget because a
    /// single visitor legitimately fires several per page, and dropping a beacon
    /// costs only a lost statistic.
    /// </summary>
    public const string Telemetry = "telemetry";

    /// <summary>
    /// Client-side error reports. The browser script already caps itself at 5 per
    /// page load; this bounds a hostile caller that ignores that.
    /// </summary>
    public const string ClientLog = "client-log";

    /// <summary>
    /// Cache administration. Called by the CMS, not by browsers, so a small budget
    /// is plenty and it blunts brute-forcing the shared secret.
    /// </summary>
    public const string CacheAdmin = "cache-admin";
}
