using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Data;

/// <summary>
/// Builds a fresh <see cref="ApplicationDbContext"/> per call, from the same
/// <see cref="DbContextOptions{TContext}"/> the shared, scoped registration uses —
/// deliberately NOT pooled (see <c>PersistenceServiceRegistration</c> for why
/// pooling this context specifically has already caused a production-only issue
/// once) and NOT the shared instance the rest of the request pipeline reads/writes
/// through, so a caller here never needs the per-circuit concurrency gate that
/// instance requires.
/// </summary>
internal sealed class CmsApplicationDbContextFactory(
    DbContextOptions<ApplicationDbContext> options, IUserContext userContext) : ICmsApplicationDbContextFactory
{
    // No CancellationToken parameter: there was one, and it was ignored here in
    // full — nothing between opening the context and disposing it can observe a
    // token. Callers pass theirs straight to the EF calls inside their own lambda,
    // which is where cancellation actually happens, so the parameter only claimed
    // a guarantee this type never made.
    public async Task ExecuteAsync(Func<ICmsApplicationDbContext, Task> operation)
    {
        await using ApplicationDbContext db = new(options, userContext);
        await operation(db);
    }

    public async Task<T> ExecuteAsync<T>(Func<ICmsApplicationDbContext, Task<T>> operation)
    {
        await using ApplicationDbContext db = new(options, userContext);
        return await operation(db);
    }
}
