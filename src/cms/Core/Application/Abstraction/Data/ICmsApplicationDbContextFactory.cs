namespace Application.Abstraction.Data;

/// <summary>
/// Creates a short-lived <see cref="ICmsApplicationDbContext"/> independent of the
/// one shared, scoped instance the rest of the request pipeline uses — for a
/// handler that needs to read the database but must NOT be serialized behind the
/// per-circuit concurrency gate that instance requires (see
/// <c>DbConcurrencyGuardPipelineBehavior</c>/<c>IBypassDbConcurrencyGuard</c>).
/// <para>
/// Meant for occasional, isolated reads — a periodic health probe, for instance —
/// not as a general alternative to constructor-injecting
/// <see cref="ICmsApplicationDbContext"/>. Each call opens and disposes its own
/// context; using it inside a hot path would just trade one cost for another.
/// </para>
/// <para>
/// <b>Why this exists rather than EF Core's own <c>IDbContextFactory&lt;T&gt;</c>:</b>
/// that interface is generic over the CONCRETE context type, and
/// <c>ApplicationDbContext</c> is <c>internal sealed</c> inside the Persistence
/// assembly — which this project does not even reference (Application.csproj sees
/// only Domain and SharedKernel). So a handler here can neither name the type nor
/// load it. The abstraction is the layering boundary, not ceremony on top of one.
/// The implementation is free to build its context however it likes; today it is a
/// plain constructor call, which is already less machinery than registering EF's
/// factory would be.
/// </para>
/// <para>
/// The callback shape is the other deliberate part: it owns disposal, so the
/// abstraction never has to expose <c>IAsyncDisposable</c>. If it did, every
/// handler injecting the ordinary shared <see cref="ICmsApplicationDbContext"/>
/// would suddenly be able to <c>await using</c> it — and disposing the context DI
/// owns would break the rest of that request.
/// </para>
/// </summary>
public interface ICmsApplicationDbContextFactory
{
    Task ExecuteAsync(Func<ICmsApplicationDbContext, Task> operation);

    Task<T> ExecuteAsync<T>(Func<ICmsApplicationDbContext, Task<T>> operation);
}
