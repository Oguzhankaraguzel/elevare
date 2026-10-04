namespace WebMvc.RateLimiting;

/// <summary>
/// Per-policy request budgets, bound from <c>appsettings.json → RateLimiting</c>.
/// Every limit is per client IP per window, so raising one does not weaken the others.
/// <para>
/// The defaults are sized for real human traffic with a wide margin; they exist to
/// stop abuse (form spam, telemetry floods, secret brute-forcing), not to shape
/// legitimate load. If a limit ever fires for a real visitor, raise it here — no
/// code change is needed.
/// </para>
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Master switch. Off only makes sense for local debugging.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Whether to read the client IP from <c>X-Forwarded-For</c> instead of the socket.
    /// <para>
    /// Leave this FALSE unless the app genuinely sits behind a reverse proxy or CDN.
    /// Behind a proxy every request arrives from the proxy's single IP, which would
    /// make all visitors share one bucket; without a proxy the header is attacker-
    /// controlled and would let anyone mint a fresh bucket per request. Enabling it
    /// is therefore a deployment-topology statement, and it is only safe when the
    /// proxy overwrites the header (which Cloudflare, nginx with
    /// <c>proxy_set_header</c>, and Azure Front Door all do).
    /// </para>
    /// </summary>
    public bool TrustForwardedForHeader { get; init; }

    /// <summary>Window length shared by all policies, in seconds.</summary>
    public int WindowSeconds { get; init; } = 60;

    /// <summary>Form submissions allowed per IP per window.</summary>
    public int FormSubmitPermitLimit { get; init; } = 10;

    /// <summary>Search requests allowed per IP per window.</summary>
    public int SearchPermitLimit { get; init; } = 120;

    /// <summary>Telemetry beacons (view/duration/click) allowed per IP per window.</summary>
    public int TelemetryPermitLimit { get; init; } = 300;

    /// <summary>Client-side error reports allowed per IP per window.</summary>
    public int ClientLogPermitLimit { get; init; } = 30;

    /// <summary>Cache administration calls allowed per IP per window.</summary>
    public int CacheAdminPermitLimit { get; init; } = 20;
}
