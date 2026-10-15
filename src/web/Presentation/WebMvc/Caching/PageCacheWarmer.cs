using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Application.Services;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicPages;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;

namespace WebMvc.Caching;

/// <summary>
/// Renders the site's pages in the background after its page cache was emptied —
/// at start-up and after every eviction the CMS asks for — so the next visitor gets
/// a cached page instead of paying for the render.
/// </summary>
/// <remarks>
/// <para>
/// A cold render is the slow one: right after a deploy every code path is compiled
/// on first use, and on a quiet site most visits used to land on a page whose cache
/// had just been dropped. Measured locally, the first render after a start took
/// ~600 ms against ~50 ms once warm.
/// </para>
/// <para>
/// It requests every published page from this same process, through the whole
/// pipeline, so what lands in the cache is exactly what a visitor would have caused.
/// The cache key and the page's own absolute links depend on the address a visitor
/// used, so the requests carry the host and scheme real visitors arrived with
/// (<see cref="RecordOrigin"/>) — until one has, there is nothing to warm for.
/// Pages still cached answer from the cache, so warming the whole site after a
/// narrow eviction re-renders only what was dropped.
/// </para>
/// </remarks>
public sealed class PageCacheWarmer(
    IServiceScopeFactory scopeFactory,
    IServer server,
    IHttpClientFactory httpClientFactory,
    IMaintenanceState maintenanceState,
    ILogger<PageCacheWarmer> logger) : BackgroundService
{
    /// <summary>Marks the warmer's own requests, so they are not taken for visitors.</summary>
    public const string RequestHeader = "X-Elevare-Warmup";

    public const string HttpClientName = "page-cache-warmer";

    private const int MaxOrigins = 16;
    private const int Parallelism = 2;
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

    private static readonly string[] SystemSlugs =
        [SystemPageProvider.NotFoundSlug, SystemPageProvider.ServerErrorSlug, SystemPageProvider.MaintenanceSlug];

    private readonly ConcurrentDictionary<string, int> _origins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private int _seenVisitor;

    /// <summary>
    /// Notes the address a visitor reached a page at. The first one after start-up
    /// starts the initial warm — before it there is no address to warm for.
    /// </summary>
    public void RecordOrigin(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Headers.ContainsKey(RequestHeader) || !request.Host.HasValue)
            return;

        string origin = $"{request.Scheme}://{request.Host.Value}";
        if (_origins.Count < MaxOrigins || _origins.ContainsKey(origin))
            _origins.AddOrUpdate(origin, 1, (_, count) => count + 1);

        if (Interlocked.Exchange(ref _seenVisitor, 1) == 0)
            RequestWarmup();
    }

    /// <summary>Asks for a warm-up soon; requests made while one is pending are folded into it.</summary>
    public void RequestWarmup() => _signal.Writer.TryWrite(true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await _signal.Reader.WaitToReadAsync(stoppingToken))
            {
                // Let a burst of evictions settle into one pass.
                await Task.Delay(Settle, stoppingToken);
                _signal.Reader.TryRead(out _);

                try
                {
                    await WarmAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Page cache warm-up failed.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task WarmAsync(CancellationToken cancellationToken)
    {
        if (maintenanceState.IsEnabled)
            return;

        // The address most visitors use: a stray Host header cannot outvote it.
        string? origin = _origins.OrderByDescending(o => o.Value).Select(o => o.Key).FirstOrDefault();
        string? self = OwnAddress();
        if (origin is null || self is null)
            return;

        List<string> paths = await PublishedPathsAsync(cancellationToken);
        Uri publicOrigin = new(origin);
        HttpClient client = httpClientFactory.CreateClient(HttpClientName);

        var watch = Stopwatch.StartNew();
        int failed = 0;
        await Parallel.ForEachAsync(
            paths,
            new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken },
            async (path, ct) =>
            {
                using HttpRequestMessage request = new(HttpMethod.Get, self + path);
                request.Headers.Host = publicOrigin.Authority;
                request.Headers.Add("X-Forwarded-Proto", publicOrigin.Scheme);
                request.Headers.Add(RequestHeader, "1");
                try
                {
                    using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (!response.IsSuccessStatusCode)
                        Interlocked.Increment(ref failed);
                }
                catch (HttpRequestException)
                {
                    Interlocked.Increment(ref failed);
                }
            });

        logger.LogInformation(
            "Warmed the page cache: {Count} pages in {Elapsed} ms ({Failed} not cached).",
            paths.Count, watch.ElapsedMilliseconds, failed);
    }

    private async Task<List<string>> PublishedPathsAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IPublicReadDbContext db = scope.ServiceProvider.GetRequiredService<IPublicReadDbContext>();

        List<int> languages = await db.Languages
            .Where(PublicLanguage.PubliclyVisible)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        List<string> slugs = await db.PageInfos
            .Where(p => p.PageStatus == PublicPageStatus.Published && p.IsActive
                && languages.Contains(p.LanguageId)
                && !(p.ParentPageId == null && SystemSlugs.Contains(p.Slug)))
            .Select(p => p.FullSlug)
            .ToListAsync(cancellationToken);

        // Homepages and section pages first: they are the ones visited most.
        return [.. slugs.OrderBy(s => s.Count(c => c == '/')).ThenBy(s => s.Length, Comparer<int>.Default).Select(s => "/" + s)];
    }

    /// <summary>This process's own plain-HTTP address, on loopback.</summary>
    private string? OwnAddress()
    {
        ICollection<string>? addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        string? http = addresses?.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
        if (http is null)
            return null;

        // "http://+:8080", "http://[::]:8080", "http://localhost:5000" — only the port matters.
        int colon = http.LastIndexOf(':');
        return colon > "http://".Length ? "http://127.0.0.1" + http[colon..].TrimEnd('/') : null;
    }
}
