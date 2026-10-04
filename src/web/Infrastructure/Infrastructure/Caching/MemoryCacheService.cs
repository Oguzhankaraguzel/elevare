using System.Collections.Concurrent;
using Application.Abstraction.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Infrastructure.Caching;

/// <summary>
/// Default <see cref="ICacheService"/> — an in-process <see cref="MemoryCache"/>
/// owned exclusively by this service (never the shared DI <c>IMemoryCache</c>
/// singleton other code might use), so <see cref="ClearAsync"/> can safely wipe
/// every tracked key without affecting anything unrelated. Keys are tracked in a
/// side set because <c>MemoryCache</c> itself has no "clear all" API.
/// </summary>
internal sealed class MemoryCacheService : ICacheService, IDisposable
{
    private readonly MemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _keys = new();

    public MemoryCacheService(IOptions<CacheOptions> options)
    {
        long sizeLimit = options.Value.MemorySizeLimitEntries ?? ComputeDefaultSizeLimit();
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit });
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_cache.TryGetValue(key, out T? value) ? value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl, Size = 1 });
        _keys[key] = 0;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        foreach (string key in _keys.Keys)
            _cache.Remove(key);
        _keys.Clear();
        return Task.CompletedTask;
    }

    // An in-process cache has no connection to lose, so it is always healthy.
    // The count is of TRACKED keys, which can briefly exceed the number of live
    // entries (a key stays tracked until its next Remove/Clear even after its
    // TTL expires) — close enough for an operator-facing number.
    public Task<CacheStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new CacheStatus("Memory", Healthy: true, Error: null, _keys.Count));

    private static long ComputeDefaultSizeLimit()
    {
        long availableBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (availableBytes <= 0)
            return 2000; // couldn't determine host memory (e.g. some container setups) — a safe flat default

        long budget = (long)(availableBytes * 0.25 / CacheOptions.AssumedAverageEntryBytes);
        return Math.Clamp(budget, 100, 100_000);
    }

    public void Dispose() => _cache.Dispose();
}
