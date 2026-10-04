using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// In-memory snapshot of the CMS-owned language list, refreshed periodically by
/// <c>SiteStateRefreshHostedService</c>. Route constraints and the language
/// switcher consult it on every request, which is why it is a snapshot rather
/// than a query.
/// </summary>
public interface ILanguageDirectory
{
    /// <summary>
    /// Whether the code names a known, active language. A pure lookup against the
    /// current snapshot — deliberately not a <see cref="Result"/>, since it cannot
    /// fail and an unknown code is the answer, not an error.
    /// </summary>
    bool IsKnownLanguageCode(string code);

    string DefaultLanguageCode { get; }

    /// <summary>
    /// Reloads the snapshot from the database. Failure leaves the previous snapshot
    /// untouched and is reported rather than thrown, because the caller (startup
    /// warm-up or the background refresher) wants to log and carry on, not crash.
    /// </summary>
    Task<Result> RefreshAsync(CancellationToken cancellationToken = default);
}
