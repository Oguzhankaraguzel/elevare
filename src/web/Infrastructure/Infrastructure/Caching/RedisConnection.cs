using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Caching;

/// <summary>
/// Default <see cref="IRedisConnection"/>. Connects on first use with
/// <c>AbortOnConnectFail=false</c>, so an unreachable server yields a multiplexer
/// that keeps reconnecting in the background instead of an exception, and reports
/// "not available" until that reconnect actually lands.
/// <para>
/// A genuinely broken configuration (bad connection string) is caught, remembered,
/// and retried no more often than <see cref="RetryInterval"/> — otherwise every
/// request would pay the full connect timeout again.
/// </para>
/// </summary>
internal sealed class RedisConnection : IRedisConnection, IDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Applied unless the connection string sets them explicitly. Deliberately
    /// short: these bound how long the very first request (the one that discovers
    /// Redis is down) can stall before the circuit opens.
    /// </summary>
    private const int DefaultConnectTimeoutMs = 1000;
    private const int DefaultSyncTimeoutMs = 1000;

    private readonly string _connectionString;
    private readonly ILogger<RedisConnection> _logger;
    private readonly Lock _gate = new();

    private ConnectionMultiplexer? _multiplexer;
    private string? _lastError;
    private DateTime _nextAttemptUtc = DateTime.MinValue;
    private bool _loggedDisconnect;

    public RedisConnection(IOptions<RedisOptions> options, ILogger<RedisConnection> logger)
    {
        _connectionString = options.Value.ConnectionString;
        _logger = logger;
    }

    public IConnectionMultiplexer? TryGetConnected(out string? error)
    {
        error = null;
        ConnectionMultiplexer? existing = _multiplexer;
        if (existing is null)
        {
            existing = TryConnect(out error);
            if (existing is null)
                return null;
        }

        if (existing.IsConnected)
        {
            if (_loggedDisconnect)
            {
                _loggedDisconnect = false;
                _lastError = null;
                _logger.LogInformation("Redis connection restored — caching is active again.");
            }

            error = null;
            return existing;
        }

        // Reachable-in-principle but not connected at this instant: report it and
        // let the caller skip Redis rather than queue a command that will time out.
        _lastError ??= "Redis is configured but no connection is currently active (reconnecting in the background).";
        if (!_loggedDisconnect)
        {
            _loggedDisconnect = true;
            _logger.LogWarning(
                "Redis is not connected — the site is serving without a distributed cache until it recovers. Detail: {Detail}",
                _lastError);
        }

        error = _lastError;
        return null;
    }

    private ConnectionMultiplexer? TryConnect(out string? error)
    {
        lock (_gate)
        {
            if (_multiplexer is not null)
            {
                error = null;
                return _multiplexer;
            }

            if (DateTime.UtcNow < _nextAttemptUtc)
            {
                error = _lastError;
                return null;
            }

            _nextAttemptUtc = DateTime.UtcNow.Add(RetryInterval);
            try
            {
                var config = ConfigurationOptions.Parse(_connectionString);
                // The whole point of this class: never abort, never throw for a
                // server that is merely down right now.
                config.AbortOnConnectFail = false;
                // RedisCacheService.ClearAsync issues FLUSHDB, which StackExchange.Redis
                // refuses to send at all unless the connection opted into admin commands —
                // without this, the CMS's "Clear Cache" button (and the web app's own
                // /api/cache/clear endpoint) threw RedisCommandException on every single
                // use. This connection exists only for that server-side cache, never for
                // arbitrary client input, so allowing admin commands here is safe.
                config.AllowAdmin = true;
                if (!_connectionString.Contains("connectTimeout", StringComparison.OrdinalIgnoreCase))
                    config.ConnectTimeout = DefaultConnectTimeoutMs;
                if (!_connectionString.Contains("syncTimeout", StringComparison.OrdinalIgnoreCase))
                    config.SyncTimeout = DefaultSyncTimeoutMs;

                _multiplexer = ConnectionMultiplexer.Connect(config);
                _lastError = null;
                error = null;
                return _multiplexer;
            }
            catch (Exception ex) when (ex is RedisException or ArgumentException or FormatException
                                          or System.Net.Sockets.SocketException or TimeoutException)
            {
                _lastError = $"{ex.GetType().Name}: {ex.Message}";
                _logger.LogError(ex,
                    "Could not establish the Redis connection — the site will run without a distributed cache until this is fixed. Retrying in {Seconds}s.",
                    RetryInterval.TotalSeconds);
                error = _lastError;
                return null;
            }
        }
    }

    public void Dispose() => _multiplexer?.Dispose();
}
