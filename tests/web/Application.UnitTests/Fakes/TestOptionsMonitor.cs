using Microsoft.Extensions.Options;

namespace Application.UnitTests.Fakes;

/// <summary>
/// A settable <see cref="IOptionsMonitor{TOptions}"/> for tests exercising code
/// that reads live options (e.g. <c>CaptchaVerifier</c>, since it needs to pick
/// up a "Sırlar" save without a restart) — <c>Options.Create</c> only produces
/// the non-reloadable <see cref="IOptions{TOptions}"/>, so it cannot stand in here.
/// <see cref="OnChange"/> is a no-op: no test so far needs to assert a live swap
/// mid-test, only to construct the service against a fixed value.
/// </summary>
internal sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; } = value;

    public T Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;
}
