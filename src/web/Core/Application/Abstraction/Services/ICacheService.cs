namespace Application.Abstraction.Services;

/// <summary>
/// Application-level cache for expensive, frequently-read queries (page
/// resolution, site settings) — distinct from ASP.NET Core's response-level
/// <c>OutputCache</c>. Backed by an in-process <c>MemoryCache</c> by default, or
/// Redis when <c>Redis:ConnectionString</c> is configured (appsettings.json) —
/// an infrastructure/deployment choice, not something the CMS can flip live.
/// <para>
/// <b>Fail-open contract:</b> every member below MUST swallow backend faults and
/// degrade to "cache miss" rather than throwing. A cache is an optimization; an
/// unreachable Redis must never turn into a failed page request. Callers are
/// therefore allowed to treat these calls as infallible. Use
/// <see cref="GetStatusAsync"/> to surface a degraded backend to operators.
/// </para>
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Drops every cached entry — backs the CMS's "Temizle" cache action.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>Live backend health, for the CMS cache admin page.</summary>
    Task<CacheStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
