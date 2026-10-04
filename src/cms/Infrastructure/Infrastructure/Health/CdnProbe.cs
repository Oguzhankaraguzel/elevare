using System.Diagnostics;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Infrastructure.Health;

/// <summary>
/// Pulls one real media file through the candidate CDN and compares it with the copy
/// on disk. Modelled on <see cref="PublicSiteProbe"/>, but deliberately NOT cached:
/// this runs only when an operator presses a button, and the whole point is to see
/// the effect of the value they just typed.
/// </summary>
internal sealed class CdnProbe(
    HttpClient httpClient,
    ICmsApplicationDbContext db,
    ILogger<CdnProbe> logger) : ICdnProbe
{
    /// <summary>
    /// Generous compared with the health probe's 5s: a cold CDN edge has to fetch
    /// from the origin before it can answer, and timing that out would report a
    /// working CDN as broken on its very first request.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Headers that tell you whether the edge is really caching or just proxying.
    /// Different providers use different ones and none is standard, so all the
    /// common spellings are checked and whichever answers first is reported.
    /// </summary>
    private static readonly string[] CacheHeaderNames =
        ["x-cache", "cf-cache-status", "x-cache-status", "x-served-by", "age"];

    public async Task<Result<CdnProbeResult>> CheckAsync(string cdnBaseUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cdnBaseUrl))
            return Result.Failure<CdnProbeResult>(CdnProbeErrors.NotConfigured);

        if (!Uri.TryCreate(cdnBaseUrl.TrimEnd('/'), UriKind.Absolute, out Uri? baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
        {
            return Result.Failure<CdnProbeResult>(CdnProbeErrors.InvalidBaseUrl(cdnBaseUrl));
        }

        // The operator types this URL and the server then fetches it, so a value aimed
        // at an internal address (127.0.0.1, 169.254.169.254, the LAN) would make the
        // "test" button an SSRF probe. A real CDN is always a public host, so refusing
        // private/reserved targets costs nothing legitimate.
        if (!OutboundUrlPolicy.IsPublicHttpUrl(baseUri.ToString()))
            return Result.Failure<CdnProbeResult>(CdnProbeErrors.BlockedPrivateHost(cdnBaseUrl));

        // A real uploaded file, not a synthetic path: the failure this catches is a
        // CDN whose origin does not resolve to THIS site's storage, and only a file
        // that genuinely exists here can distinguish that from a plain 404.
        MediaFile? sample = await db.MediaFiles
            .AsNoTracking()
            .Where(m => !m.IsDeleted && m.FileSize > 0)
            .OrderByDescending(m => m.CreateDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (sample is null)
            return Result.Failure<CdnProbeResult>(CdnProbeErrors.NoMediaToTest);

        // FilePath is stored as the public path ("/uploads/x.jpg" or, once a CDN was
        // already configured when it was uploaded, a full URL). Only the trailing
        // relative part is ours to re-prefix.
        string relativePath = ToRelativePath(sample.FilePath);
        string testedUrl = $"{baseUri.ToString().TrimEnd('/')}/{relativePath}";

        return await ProbeAsync(testedUrl, sample.FileSize, cancellationToken);
    }

    private async Task<Result<CdnProbeResult>> ProbeAsync(string url, long expectedBytes, CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            // GET with headers-only completion, not HEAD: CDNs routinely answer HEAD
            // from a different code path (or refuse it outright) while GET works fine,
            // and Content-Length from a HEAD is not evidence about what a visitor gets.
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            using HttpResponseMessage response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            long? servedBytes = response.Content.Headers.ContentLength;

            // Chunked responses carry no Content-Length; read the body to find out
            // rather than reporting "unknown size" as a mismatch.
            if (servedBytes is null && response.IsSuccessStatusCode)
            {
                byte[] body = await response.Content.ReadAsByteArrayAsync(timeout.Token);
                servedBytes = body.LongLength;
            }

            return Result.Success(new CdnProbeResult(
                Reachable: true,
                StatusCode: (int)response.StatusCode,
                Error: null,
                ResponseTime: Stopwatch.GetElapsedTime(startedAt),
                TestedUrl: url,
                ExpectedBytes: expectedBytes,
                ServedBytes: servedBytes,
                CacheHeader: ReadCacheHeader(response)));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            if (ex is TaskCanceledException && cancellationToken.IsCancellationRequested)
                throw;

            logger.LogWarning(ex, "CDN probe failed for {Url}.", url);

            return Result.Success(new CdnProbeResult(
                Reachable: false,
                StatusCode: null,
                Error: $"{ex.GetType().Name}: {ex.Message}",
                ResponseTime: Stopwatch.GetElapsedTime(startedAt),
                TestedUrl: url,
                ExpectedBytes: expectedBytes,
                ServedBytes: null,
                CacheHeader: null));
        }
    }

    private static string? ReadCacheHeader(HttpResponseMessage response)
    {
        foreach (string name in CacheHeaderNames)
        {
            if (response.Headers.TryGetValues(name, out IEnumerable<string>? values))
                return $"{name}: {string.Join(", ", values)}";
        }
        return null;
    }

    private static string ToRelativePath(string filePath)
    {
        if (Uri.TryCreate(filePath, UriKind.Absolute, out Uri? absolute))
            return absolute.AbsolutePath.TrimStart('/');

        return filePath.TrimStart('/');
    }
}
