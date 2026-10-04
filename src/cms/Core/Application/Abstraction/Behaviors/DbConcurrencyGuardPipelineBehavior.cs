using MediatR;
using SharedKernel.Abstraction.Messaging;

namespace Application.Abstraction.Behaviors;

/// <summary>
/// Blazor Server components can fire off <c>Sender.Send</c> calls concurrently within
/// the same circuit (e.g. one component's <c>OnInitializedAsync</c> still awaiting a
/// multi-step query while a sibling's <c>OnAfterRenderAsync</c> starts another one) —
/// all sharing the one scoped <c>ICmsApplicationDbContext</c>, which EF Core does not
/// allow to be used from two operations at once ("A second operation was started on
/// this context instance..."). This wraps every request in a per-circuit semaphore so
/// they run one at a time instead of racing and throwing.
/// <para>
/// A request implementing <see cref="IBypassDbConcurrencyGuard"/> skips the gate
/// entirely — see that interface for why holding it is not automatically safe just
/// because a handler is "read-only".
/// </para>
/// </summary>
internal sealed class DbConcurrencyGuardPipelineBehavior<TRequest, TResponse>(DbContextConcurrencyGate gate)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) =>
        request is IBypassDbConcurrencyGuard ? next() : gate.RunAsync(() => next(), cancellationToken);
}
