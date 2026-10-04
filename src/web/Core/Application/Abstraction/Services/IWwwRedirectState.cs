using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// In-memory snapshot of the CMS's "redirect to www" switch, refreshed alongside
/// <see cref="IMaintenanceState"/> and read by <c>SeoRedirectRule</c> on every request.
/// </summary>
public interface IWwwRedirectState
{
    /// <summary>
    /// Whether a request for the bare domain is sent to its <c>www.</c> address.
    /// On until the setting says otherwise.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Reloads the snapshot from the database. Failure leaves the previous value in
    /// place and is reported rather than thrown.
    /// </summary>
    Task<Result> RefreshAsync(CancellationToken cancellationToken = default);
}
