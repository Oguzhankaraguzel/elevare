using Application.Abstraction.Services;
using Infrastructure.Caching;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using WebMvc.Controllers;
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
    public static WebApplication MapCacheEndpoints(this WebApplication app)
    {
        app.MapPost("/api/cache/clear", async (
            HttpContext http, ICacheService cache, IOutputCacheStore outputCache,
            ILanguageDirectory languageDirectory, IMaintenanceState maintenanceState,
            IWwwRedirectState wwwRedirectState,
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
            await outputCache.EvictByTagAsync(PageController.OutputCacheTag, ct);
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
