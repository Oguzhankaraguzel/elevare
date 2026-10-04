namespace Application.Abstraction.Services;

/// <summary>
/// What came back when the CMS asked a candidate CDN for a file it already holds
/// on disk.
/// </summary>
/// <param name="Reachable">Whether a response arrived at all. False means DNS, TLS or the connection failed.</param>
/// <param name="StatusCode">The HTTP status when one arrived. 404 here almost always means the CDN's origin is pointed somewhere else.</param>
/// <param name="Error">The transport error verbatim, when there was one.</param>
/// <param name="ResponseTime">Round-trip time — a working CDN that is slower than the origin is worth seeing.</param>
/// <param name="TestedUrl">The exact URL that was requested, so the operator can retry it themselves.</param>
/// <param name="ExpectedBytes">Size of the local original.</param>
/// <param name="ServedBytes">Size the CDN reported/returned, when it answered.</param>
/// <param name="CacheHeader">Whatever the edge said about caching (x-cache, cf-cache-status, age…) — the difference between "proxying every request" and "actually caching".</param>
// TestedUrl is string, not Uri: it is echoed back to the operator verbatim so they
// can paste it into a browser, including the malformed case the probe rejected.
#pragma warning disable CA1054
public sealed record CdnProbeResult(
    bool Reachable,
    int? StatusCode,
    string? Error,
    TimeSpan ResponseTime,
    string TestedUrl,
    long ExpectedBytes,
    long? ServedBytes,
    string? CacheHeader)
{
    /// <summary>
    /// Answered 200 AND handed back the same number of bytes the origin holds.
    /// Status alone is not enough: a misconfigured CDN happily returns 200 with its
    /// own error page, which is the exact failure this check exists to catch.
    /// </summary>
    public bool Healthy => Reachable
        && StatusCode is >= 200 and < 300
        && ServedBytes == ExpectedBytes;
}
#pragma warning restore CA1054
