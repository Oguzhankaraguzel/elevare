using Application.Abstraction.Services;
using Application.UnitTests.Fakes;
using Infrastructure.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Exercises <see cref="RedisCacheService"/> over a real TCP socket against
/// <see cref="FakeRedisServer"/>.
/// <para>
/// The outage tests here guard a bug that shipped once already: registering
/// <c>ConnectionMultiplexer.Connect</c> as a plain DI factory made an unreachable
/// Redis throw inside the first request that touched the cache, which turned every
/// public page into an HTTP 500. Build and unit tests were both green at the time —
/// only starting the app revealed it. These tests make that failure mode
/// impossible to reintroduce silently.
/// </para>
/// </summary>
public sealed class RedisCacheServiceTests : IDisposable
{
    private const string DeadEndpoint = "127.0.0.1:6399,connectTimeout=300,syncTimeout=300,connectRetry=1";

    private readonly List<RedisConnection> _connections = [];

    private RedisCacheService CreateSut(string connectionString)
    {
        RedisConnection connection = new(
            Options.Create(new RedisOptions { ConnectionString = connectionString }),
            NullLogger<RedisConnection>.Instance);
        _connections.Add(connection);
        return new RedisCacheService(connection, NullLogger<RedisCacheService>.Instance);
    }

    public void Dispose()
    {
        foreach (RedisConnection connection in _connections)
            connection.Dispose();
    }

    [Fact]
    public async Task Set_then_get_round_trips_through_redis()
    {
        using FakeRedisServer server = new();
        RedisCacheService sut = CreateSut(server.ConnectionString);

        await sut.SetAsync("k1", "hello", TimeSpan.FromMinutes(1));

        (await sut.GetAsync<string>("k1")).ShouldBe("hello");
        server.KeyCount.ShouldBe(1);
    }

    [Fact]
    public async Task Complex_values_survive_the_json_round_trip()
    {
        using FakeRedisServer server = new();
        RedisCacheService sut = CreateSut(server.ConnectionString);
        Dictionary<string, string?> value = new() { ["General.SiteName"] = "Elevare", ["Appearance.LogoUrl"] = null };

        await sut.SetAsync("settings", value, TimeSpan.FromMinutes(1));
        Dictionary<string, string?>? read = await sut.GetAsync<Dictionary<string, string?>>("settings");

        read.ShouldNotBeNull();
        read["General.SiteName"].ShouldBe("Elevare");
        read["Appearance.LogoUrl"].ShouldBeNull();
    }

    [Fact]
    public async Task Get_on_a_missing_key_returns_default()
    {
        using FakeRedisServer server = new();
        RedisCacheService sut = CreateSut(server.ConnectionString);

        (await sut.GetAsync<string>("nope")).ShouldBeNull();
    }

    [Fact]
    public async Task Remove_deletes_the_key_on_the_server()
    {
        using FakeRedisServer server = new();
        RedisCacheService sut = CreateSut(server.ConnectionString);
        await sut.SetAsync("k1", "hello", TimeSpan.FromMinutes(1));

        await sut.RemoveAsync("k1");

        server.KeyCount.ShouldBe(0);
        (await sut.GetAsync<string>("k1")).ShouldBeNull();
    }

    [Fact]
    public async Task Clear_flushes_every_key()
    {
        using FakeRedisServer server = new();
        RedisCacheService sut = CreateSut(server.ConnectionString);
        await sut.SetAsync("a", "1", TimeSpan.FromMinutes(1));
        await sut.SetAsync("b", "2", TimeSpan.FromMinutes(1));

        await sut.ClearAsync();

        server.KeyCount.ShouldBe(0);
    }

    [Fact]
    public async Task Status_is_healthy_and_reports_the_entry_count_when_connected()
    {
        using FakeRedisServer server = new();
        RedisCacheService sut = CreateSut(server.ConnectionString);
        await sut.SetAsync("a", "1", TimeSpan.FromMinutes(1));

        CacheStatus status = await sut.GetStatusAsync();

        status.Provider.ShouldBe("Redis");
        status.Healthy.ShouldBeTrue();
        status.Error.ShouldBeNull();
        status.EntryCount.ShouldBe(1);
    }

    [Fact]
    public async Task Reads_degrade_to_a_miss_instead_of_throwing_when_redis_is_unreachable()
    {
        // Nothing is listening on this port.
        RedisCacheService sut = CreateSut(DeadEndpoint);

        string? value = await sut.GetAsync<string>("k1");

        value.ShouldBeNull();
    }

    [Fact]
    public async Task Writes_become_no_ops_instead_of_throwing_when_redis_is_unreachable()
    {
        RedisCacheService sut = CreateSut(DeadEndpoint);

        // Must not throw — the caller is entitled to treat the cache as infallible.
        await sut.SetAsync("k1", "hello", TimeSpan.FromMinutes(1));
        await sut.RemoveAsync("k1");
        await sut.ClearAsync();

        // Nothing was stored, and the failure is still reported for operators.
        (await sut.GetAsync<string>("k1")).ShouldBeNull();
        (await sut.GetStatusAsync()).Healthy.ShouldBeFalse();
    }

    [Fact]
    public async Task Status_reports_unhealthy_with_a_diagnostic_when_redis_is_unreachable()
    {
        RedisCacheService sut = CreateSut(DeadEndpoint);

        CacheStatus status = await sut.GetStatusAsync();

        status.Provider.ShouldBe("Redis");
        status.Healthy.ShouldBeFalse();
        status.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task An_outage_mid_flight_still_degrades_quietly()
    {
        FakeRedisServer server = new();
        RedisCacheService sut = CreateSut(server.ConnectionString);
        await sut.SetAsync("k1", "hello", TimeSpan.FromMinutes(1));
        (await sut.GetAsync<string>("k1")).ShouldBe("hello");

        server.Dispose(); // Redis goes away underneath a live connection.

        string? afterOutage = await sut.GetAsync<string>("k1");
        afterOutage.ShouldBeNull();
        (await sut.GetStatusAsync()).Healthy.ShouldBeFalse();
    }

    [Fact]
    public async Task A_malformed_connection_string_does_not_throw_at_use_time()
    {
        RedisCacheService sut = CreateSut("this is not a redis endpoint:::");

        (await sut.GetAsync<string>("k1")).ShouldBeNull();
        (await sut.GetStatusAsync()).Healthy.ShouldBeFalse();
    }
}
