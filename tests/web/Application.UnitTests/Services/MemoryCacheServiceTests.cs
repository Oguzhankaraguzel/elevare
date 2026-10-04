using Infrastructure.Caching;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Covers <see cref="MemoryCacheService"/> — the default <c>ICacheService</c>.
/// Key tracking matters here specifically because <c>MemoryCache</c> itself has
/// no built-in "clear all" API; <see cref="MemoryCacheService.ClearAsync"/> only
/// works because every <c>SetAsync</c> records its key in a side set first.
/// </summary>
public sealed class MemoryCacheServiceTests
{
    private static MemoryCacheService CreateSut() =>
        new(Options.Create(new CacheOptions { MemorySizeLimitEntries = 100 }));

    [Fact]
    public async Task Set_then_get_returns_the_stored_value()
    {
        using MemoryCacheService sut = CreateSut();

        await sut.SetAsync("k1", "hello", TimeSpan.FromMinutes(1));

        (await sut.GetAsync<string>("k1")).ShouldBe("hello");
    }

    [Fact]
    public async Task Get_on_a_missing_key_returns_default()
    {
        using MemoryCacheService sut = CreateSut();

        (await sut.GetAsync<string>("missing")).ShouldBeNull();
    }

    [Fact]
    public async Task Remove_deletes_the_entry()
    {
        using MemoryCacheService sut = CreateSut();
        await sut.SetAsync("k1", "hello", TimeSpan.FromMinutes(1));

        await sut.RemoveAsync("k1");

        (await sut.GetAsync<string>("k1")).ShouldBeNull();
    }

    [Fact]
    public async Task Clear_removes_every_tracked_entry()
    {
        using MemoryCacheService sut = CreateSut();
        await sut.SetAsync("k1", "a", TimeSpan.FromMinutes(1));
        await sut.SetAsync("k2", "b", TimeSpan.FromMinutes(1));

        await sut.ClearAsync();

        (await sut.GetAsync<string>("k1")).ShouldBeNull();
        (await sut.GetAsync<string>("k2")).ShouldBeNull();
    }

    [Fact]
    public async Task An_expired_entry_is_no_longer_returned()
    {
        using MemoryCacheService sut = CreateSut();

        await sut.SetAsync("k1", "hello", TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        (await sut.GetAsync<string>("k1")).ShouldBeNull();
    }
}
