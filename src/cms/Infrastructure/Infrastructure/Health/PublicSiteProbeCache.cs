using Application.Abstraction.Services;

namespace Infrastructure.Health;

/// <summary>
/// Holds the last public-site probe result so every Blazor circuit shares one.
/// <para>
/// A singleton rather than static fields on the probe itself: the probe is created
/// per scope (it needs a DbContext), so its own state would reset constantly, and
/// static mutable state on a scoped service is the kind of thing that works until
/// two requests overlap. Making the shared lifetime explicit is also what lets a
/// test hand in a fresh cache instead of fighting leftovers from a previous run.
/// </para>
/// </summary>
internal sealed class PublicSiteProbeCache
{
    /// <summary>Only one probe may be in flight; the rest wait and reuse its answer.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    private PublicSiteStatus? _status;
    private DateTime _storedAtUtc = DateTime.MinValue;

    /// <summary>The cached status if it is still within <paramref name="freshFor"/>, otherwise null.</summary>
    public PublicSiteStatus? Read(TimeSpan freshFor) =>
        _status is not null && DateTime.UtcNow - _storedAtUtc < freshFor ? _status : null;

    public void Store(PublicSiteStatus status)
    {
        _status = status;
        _storedAtUtc = DateTime.UtcNow;
    }
}
