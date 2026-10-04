using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// In-memory snapshot of the CMS's maintenance-mode switch, refreshed periodically
/// by <c>SiteStateRefreshHostedService</c> and read by
/// <c>MaintenanceModeMiddleware</c> on every request.
/// </summary>
public interface IMaintenanceState
{
    /// <summary>
    /// Whether maintenance mode is on, per the current snapshot. A field read —
    /// deliberately not a <see cref="Result"/>, since a request-path gate has no
    /// useful "errored" state to act on.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Reloads the snapshot from the database. Failure leaves the previous value
    /// in place and is reported rather than thrown (see
    /// <see cref="ILanguageDirectory.RefreshAsync"/> for the same reasoning).
    /// </summary>
    Task<Result> RefreshAsync(CancellationToken cancellationToken = default);
}
