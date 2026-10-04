using Application.Abstraction.Services;

namespace Application.UnitTests;

/// <summary>Always-miss <see cref="ICacheService"/> double — keeps handler tests
/// exercising the real DB path instead of a cache hit.</summary>
internal sealed class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<CacheStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new CacheStatus("NoOp", Healthy: true, Error: null, EntryCount: 0));
}
