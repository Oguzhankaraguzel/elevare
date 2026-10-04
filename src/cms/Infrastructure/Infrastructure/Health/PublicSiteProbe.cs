using System.Diagnostics;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Infrastructure.Health;

/// <summary>
/// Sends a HEAD request to the public site's root and reports what came back.
/// <para>
/// The result is cached for a few seconds. Every CMS user's header polls health on
/// its own timer, and a room full of editors must not turn into a load test against
/// the very site being checked — especially while it is already struggling.
/// </para>
/// <para>
/// Reads through <see cref="ICmsApplicationDbContextFactory"/> rather than the
/// shared scoped context: this is called from <c>GetSiteHealthQueryHandler</c>,
/// which opts out of the per-circuit DbContext-concurrency gate
/// (<c>IBypassDbConcurrencyGuard</c>) so that the outbound HTTP probe below —
/// up to <see cref="Timeout"/> — never blocks the rest of the circuit.
/// </para>
/// </summary>
internal sealed class PublicSiteProbe(
    HttpClient httpClient,
    ICmsApplicationDbContextFactory dbFactory,
    PublicSiteProbeCache cache,
    ILogger<PublicSiteProbe> logger) : IPublicSiteProbe
{
    private const string PublicSiteBaseUrlKey = "Advanced.PublicSiteBaseUrl";

    /// <summary>
    /// Long enough that concurrent editors share one probe, short enough that an
    /// outage still surfaces within a poll cycle or two.
    /// </summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(15);

    /// <summary>
    /// A site that has not answered in this long is down as far as a visitor is
    /// concerned, and the header must not hang waiting to prove it.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<Result<PublicSiteStatus>> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (cache.Read(CacheFor) is { } fresh)
            return Result.Success(fresh);

        string? baseUrl = await dbFactory.ExecuteAsync(
            db => db.SiteSettings
                .Where(s => s.Key == PublicSiteBaseUrlKey && !s.IsDeleted)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(cancellationToken));

        // Not configured is reported by the settings probe in the health handler;
        // saying it twice would just be noise.
        if (string.IsNullOrWhiteSpace(baseUrl))
            return Result.Failure<PublicSiteStatus>(
                PublicSiteProbeErrors.BaseUrlNotConfigured);

        await cache.Gate.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have filled the cache while this one queued.
            if (cache.Read(CacheFor) is { } justFilled)
                return Result.Success(justFilled);

            PublicSiteStatus status = await ProbeAsync(baseUrl.TrimEnd('/'), cancellationToken);
            cache.Store(status);
            return Result.Success(status);
        }
        finally
        {
            cache.Gate.Release();
        }
    }

    private async Task<PublicSiteStatus> ProbeAsync(string baseUrl, CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            // HEAD, not GET: the question is "are you serving", and pulling the whole
            // home page every 15 seconds to answer it would be rude to a live site.
            using HttpRequestMessage request = new(HttpMethod.Head, baseUrl);
            using HttpResponseMessage response = await httpClient.SendAsync(request, timeout.Token);

            return new PublicSiteStatus(
                Reachable: true,
                StatusCode: (int)response.StatusCode,
                Error: null,
                ResponseTime: Stopwatch.GetElapsedTime(startedAt));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            // A cancel that came from OUR timeout means unreachable; one that came
            // from the caller means the page went away, and that is not a finding.
            if (ex is TaskCanceledException && cancellationToken.IsCancellationRequested)
                throw;

            logger.LogWarning(ex, "Public site probe failed for {BaseUrl}.", baseUrl);

            return new PublicSiteStatus(
                Reachable: false,
                StatusCode: null,
                Error: $"{ex.GetType().Name}: {ex.Message}",
                ResponseTime: Stopwatch.GetElapsedTime(startedAt));
        }
    }
}
