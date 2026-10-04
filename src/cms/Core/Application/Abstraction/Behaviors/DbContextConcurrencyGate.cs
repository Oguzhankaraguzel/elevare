namespace Application.Abstraction.Behaviors;

/// <summary>
/// Serializes every MediatR request within one DI scope (one Blazor Server circuit)
/// so they never touch the shared scoped <c>ICmsApplicationDbContext</c> concurrently.
/// Registered Scoped — one gate, and therefore one semaphore, per circuit.
/// </summary>
public sealed class DbContextConcurrencyGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _semaphore.Dispose();
}
