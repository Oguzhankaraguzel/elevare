using Application.Abstraction.Services;
using Infrastructure.Caching;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using SharedKernel.Content;
using WebMvc.Caching;
using WebMvc.RateLimiting;

namespace WebMvc.Endpoints;

/// <summary>
/// Lets the CMS's cache admin page reach across into the Web app's own in-process
/// or Redis cache — the two apps otherwise share nothing but the database.
/// Protected by a shared secret (<c>Cache:ClearSecret</c>, must match the CMS's
/// own copy) rather than a user login, since the caller is a server, not a browser
/// — see <see cref="InternalAdminAuthorization"/>, shared with <c>SecretsEndpoints</c>.
/// </summary>
public static class CacheEndpoints
{
    // A save touches a page, its group and a handful of shared tags; a bulk edit a
    // few hundred pages. Past this, the caller should be clearing everything.
    private const int MaxTags = 2000;

    /// <summary>The body of <c>/api/cache/evict</c>.</summary>
    public sealed record EvictRequest(IReadOnlyList<string>? Tags);

    public static WebApplication MapCacheEndpoints(this WebApplication app)
    {
        app.MapPost("/api/cache/clear", async (
            HttpContext http, ICacheService cache, IOutputCacheStore outputCache,
            ILanguageDirectory languageDirectory, IMaintenanceState maintenanceState,
            IWwwRedirectState wwwRedirectState, PageCacheWarmer warmer,
            IOptions<CacheOptions> options, CancellationToken ct) =>
        {
            if (!InternalAdminAuthorization.IsAuthorized(http, options.Value.ClearSecret))
                return Results.Unauthorized();

            // Everything a page is rendered from first, the rendered pages last — the
            // other order could let a request in between re-cache a page from stale
            // data for the whole output-cache lifetime. That includes the language and
            // maintenance snapshots: they refresh on their own only every 30 seconds,
            // and a page rendered in that gap would keep, say, a language switcher
            // without the language that was just published. A failed refresh keeps
            // the previous snapshot, as the periodic one does.
            await cache.ClearAsync(ct);
            await languageDirectory.RefreshAsync(ct);
            await maintenanceState.RefreshAsync(ct);
            await wwwRedirectState.RefreshAsync(ct);
            await outputCache.EvictByTagAsync(PageCacheTags.All, ct);
            warmer.RequestWarmup();
            return Results.NoContent();
        }).RequireRateLimiting(RateLimitPolicies.CacheAdmin);

        // What the CMS sends after an ordinary save: only the pages built from what
        // it changed (see PageCacheTags). Data caches are cleared whole — cheap, and
        // the renders that follow must not read a stale copy. The language and
        // maintenance snapshots are left alone: a change to either is never
        // narrowed down, so it arrives at /clear above.
        app.MapPost("/api/cache/evict", async (
            HttpContext http, EvictRequest body, ICacheService cache, IOutputCacheStore outputCache,
            PageCacheWarmer warmer, ILoggerFactory loggers, IOptions<CacheOptions> options, CancellationToken ct) =>
        {
            if (!InternalAdminAuthorization.IsAuthorized(http, options.Value.ClearSecret))
                return Results.Unauthorized();
            if (body.Tags is not { Count: > 0 and <= MaxTags })
                return Results.BadRequest();

            await cache.ClearAsync(ct);
            string[] tags = [.. body.Tags.Distinct(StringComparer.Ordinal)];
            foreach (string tag in tags)
                await outputCache.EvictByTagAsync(tag, ct);
            loggers.CreateLogger(typeof(CacheEndpoints)).LogInformation(
                "Evicted cached pages tagged {Tags}", string.Join(", ", tags.Take(20)));
            warmer.RequestWarmup();
            return Results.NoContent();
        }).RequireRateLimiting(RateLimitPolicies.CacheAdmin);

        // Reports which backend is live and, when it is degraded, why — so a
        // misconfigured or unreachable Redis is visible in the CMS UI instead of
        // silently costing performance (the cache itself fails open).
        app.MapGet("/api/cache/status", async (
            HttpContext http, ICacheService cache, IOptions<CacheOptions> options, CancellationToken ct) =>
        {
            if (!InternalAdminAuthorization.IsAuthorized(http, options.Value.ClearSecret))
                return Results.Unauthorized();

            return Results.Ok(await cache.GetStatusAsync(ct));
        }).RequireRateLimiting(RateLimitPolicies.CacheAdmin);

        return app;
    }
}
