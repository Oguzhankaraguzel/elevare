using StackExchange.Redis;

namespace Infrastructure.Caching;

/// <summary>
/// Lazily-established Redis connection that never throws at resolution time and
/// never blocks a request while the server is down.
/// <para>
/// Registering <c>ConnectionMultiplexer.Connect(...)</c> directly as a DI factory
/// is a trap twice over: the factory runs inside whatever request first needs the
/// cache and throws when the server is unreachable (an HTTP 500 per request), and
/// even with <c>AbortOnConnectFail=false</c> every individual command then sits in
/// the backlog until it times out — a page doing four cache operations pays four
/// timeouts and takes ~20s. This wrapper keeps both failure modes out of the
/// request path: <see cref="TryGetConnected"/> hands back a multiplexer only when
/// it is actually usable, so <c>RedisCacheService</c> can skip Redis instantly and
/// resume the moment the background reconnect succeeds.
/// </para>
/// </summary>
internal interface IRedisConnection
{
    /// <summary>
    /// A multiplexer that is connected right now, or <see langword="null"/> when
    /// Redis is unusable — in which case <paramref name="error"/> explains why.
    /// Returns immediately in both cases; it never waits on a connect attempt.
    /// </summary>
    IConnectionMultiplexer? TryGetConnected(out string? error);
}
