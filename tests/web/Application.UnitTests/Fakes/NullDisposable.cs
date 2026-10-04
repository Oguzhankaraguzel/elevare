namespace Application.UnitTests.Fakes;

/// <summary>
/// Not nested in <see cref="TestOptionsMonitor{T}"/>: a static field in a generic
/// type is a separate instance per closed type (one per <c>T</c>), which defeats
/// the point of a shared singleton (SonarSource S2743) — kept as its own,
/// non-generic type instead.
/// </summary>
internal sealed class NullDisposable : IDisposable
{
    public static readonly NullDisposable Instance = new();
    public void Dispose() { }
}
