namespace SharedKernel.Abstraction.Messaging;

/// <summary>
/// Marks a request that must NOT be serialized behind the per-circuit
/// DbContext-concurrency semaphore (see the CMS's <c>DbConcurrencyGuardPipelineBehavior</c>).
/// <para>
/// That guard exists to stop two requests in the same Blazor Server circuit from
/// touching the one shared, scoped <c>DbContext</c> at once — but it wraps the
/// ENTIRE handler, including any I/O that has nothing to do with that DbContext.
/// A handler that reads through an independent, short-lived context of its own
/// (rather than the shared one) needs none of that protection, and — if it also
/// makes a slow outbound call (an HTTP health probe, for instance) — holding the
/// gate for its whole duration would stall every other action in the same circuit
/// for as long as that call takes. Implement this on a request only once its
/// handler is verified not to touch the shared DbContext.
/// </para>
/// </summary>
public interface IBypassDbConcurrencyGuard;
