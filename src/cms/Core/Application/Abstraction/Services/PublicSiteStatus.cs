namespace Application.Abstraction.Services;

/// <summary>
/// What the public site answered when the CMS last knocked.
/// </summary>
/// <param name="Reachable">
/// Whether a response came back at all. False means the connection failed or timed
/// out — the site is down, not merely unhappy.
/// </param>
/// <param name="StatusCode">
/// The HTTP status when one arrived. A reachable site returning 500 is a different
/// problem from an unreachable one, and an operator needs to tell them apart.
/// </param>
/// <param name="Error">The transport error verbatim, when there was one.</param>
/// <param name="ResponseTime">
/// How long the round trip took. Slow-but-alive is worth surfacing before it
/// becomes down.
/// </param>
public sealed record PublicSiteStatus(
    bool Reachable,
    int? StatusCode,
    string? Error,
    TimeSpan ResponseTime)
{
    /// <summary>Reachable and not answering with an error status.</summary>
    public bool Healthy => Reachable && StatusCode is >= 200 and < 400;
}
