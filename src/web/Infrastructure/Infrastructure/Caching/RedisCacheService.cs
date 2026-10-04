using System.Text.Json;
using Application.Abstraction.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Caching;

/// <summary>
/// <see cref="ICacheService"/> backed by Redis — active when <c>Redis:ConnectionString</c>
/// is configured. Values are JSON-serialized (unlike the in-process memory cache,
/// which can store the .NET object directly) since Redis only stores bytes/strings.
/// <para>
/// Every operation is fail-open per <see cref="ICacheService"/>'s contract: a
/// connection loss degrades reads to a cache miss and turns writes into no-ops,
/// so the public site keeps serving from the database. Without this, a Redis
/// outage would surface as an HTTP 500 on every page — the cache would become a
/// single point of failure for the whole site, which is the opposite of its job.
/// </para>
/// </summary>
internal sealed class RedisCacheService : ICacheService
{
    private readonly IRedisConnection _connection;
    private readonly ILogger<RedisCacheService> _logger;

    // Remembered so the CMS cache page can show WHY the backend is unhealthy,
    // not just that it is. Written from multiple request threads, hence volatile.
    private volatile string? _lastError;

    public RedisCacheService(IRedisConnection connection, ILogger<RedisCacheService> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            IDatabase? db = GetDatabase();
            if (db is null)
                return default;

            RedisValue value = await db.StringGetAsync(key);
            return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            Degrade(ex, nameof(GetAsync), key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            IDatabase? db = GetDatabase();
            if (db is null)
                return;

            await db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            Degrade(ex, nameof(SetAsync), key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            IDatabase? db = GetDatabase();
            if (db is null)
                return;

            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            Degrade(ex, nameof(RemoveAsync), key);
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IConnectionMultiplexer? multiplexer = _connection.TryGetConnected(out string? error);
            if (multiplexer is null)
            {
                _lastError = error;
                return;
            }

            foreach (System.Net.EndPoint endpoint in multiplexer.GetEndPoints())
            {
                IServer server = multiplexer.GetServer(endpoint);
                if (server.IsConnected && !server.IsReplica)
                    await server.FlushDatabaseAsync();
            }
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            Degrade(ex, nameof(ClearAsync), key: null);
        }
    }

    public async Task<CacheStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        IConnectionMultiplexer? multiplexer = _connection.TryGetConnected(out string? connectError);
        if (multiplexer is null)
            return new CacheStatus("Redis", Healthy: false, connectError ?? _lastError, EntryCount: null);

        try
        {
            IDatabase db = multiplexer.GetDatabase();
            await db.PingAsync();

            long? entries = null;
            foreach (System.Net.EndPoint endpoint in multiplexer.GetEndPoints())
            {
                IServer server = multiplexer.GetServer(endpoint);
                if (server.IsConnected && !server.IsReplica)
                    entries = (entries ?? 0) + await server.DatabaseSizeAsync();
            }

            return new CacheStatus("Redis", Healthy: true, Error: null, entries);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            return new CacheStatus("Redis", Healthy: false, Describe(ex), EntryCount: null);
        }
    }

    private IDatabase? GetDatabase()
    {
        IConnectionMultiplexer? multiplexer = _connection.TryGetConnected(out string? error);
        if (multiplexer is not null)
            return multiplexer.GetDatabase();

        _lastError = error;
        return null;
    }

    private void Degrade(Exception ex, string operation, string? key)
    {
        _lastError = Describe(ex);
        _logger.LogWarning(ex,
            "Redis cache {Operation} failed for key '{Key}' — serving without cache. See /admin/cache in the CMS.",
            operation, key ?? "(all)");
    }

    // Backend faults we deliberately absorb. Anything else (a genuine bug in this
    // class, an OOM, …) is left to propagate rather than being hidden behind a
    // silent cache miss.
    private static bool IsRecoverable(Exception ex) =>
        ex is RedisException or System.Net.Sockets.SocketException or TimeoutException
           or JsonException or ObjectDisposedException or InvalidOperationException;

    private static string Describe(Exception ex) => $"{ex.GetType().Name}: {ex.Message}";
}
