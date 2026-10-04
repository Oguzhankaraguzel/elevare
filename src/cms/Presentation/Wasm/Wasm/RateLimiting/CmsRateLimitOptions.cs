namespace Wasm.RateLimiting;

/// <summary>
/// Request budgets for the admin panel, bound from <c>appsettings.json → RateLimiting</c>.
/// <para>
/// Far narrower than the public site's: an admin panel is used by a handful of known
/// people, and throttling their normal work would be a self-inflicted outage. Only
/// the endpoints an anonymous caller can reach are limited, which today means the
/// sign-in form.
/// </para>
/// <para>
/// This is the second line of defence, not the first. Identity's account lockout
/// (five failures, fifteen minutes) protects a single account however the attempts
/// arrive; this protects the server from someone spraying one password across many
/// accounts, which no per-account counter would ever notice.
/// </para>
/// </summary>
public sealed class CmsRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Master switch. Off only makes sense for local debugging.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Whether to read the client IP from <c>X-Forwarded-For</c> rather than the socket.
    /// <para>
    /// Leave FALSE unless the panel genuinely sits behind a reverse proxy that
    /// overwrites the header. Without such a proxy the header is attacker-controlled,
    /// and trusting it would let anyone mint a fresh bucket per request — turning the
    /// limiter off while appearing to be on.
    /// </para>
    /// </summary>
    public bool TrustForwardedForHeader { get; init; }

    /// <summary>Window length in seconds.</summary>
    public int WindowSeconds { get; init; } = 300;

    /// <summary>
    /// Sign-in attempts allowed per IP per window. Twenty in five minutes is far more
    /// than a person needs — even a bad typist authenticates in three — and far less
    /// than a password spray requires.
    /// </summary>
    public int LoginPermitLimit { get; init; } = 20;
}
