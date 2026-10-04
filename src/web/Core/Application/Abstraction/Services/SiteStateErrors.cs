using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Failures from the CMS-owned state snapshots the public site keeps in memory
/// (<see cref="ILanguageDirectory"/>, <see cref="IMaintenanceState"/>).
/// <para>
/// A failed refresh is not fatal — the previous snapshot stays in place and the
/// background refresher will try again — but it does mean the site is serving
/// data that is getting older, which is worth a log line rather than silence.
/// </para>
/// </summary>
public static class SiteStateErrors
{
    public static Error RefreshFailed(string snapshot, string detail) =>
        Error.Problem("SiteState.RefreshFailed", $"Refreshing the {snapshot} snapshot failed: {detail}");
}
