using Application.Abstraction.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Application.Services;

/// <summary>
/// Keeps the Web app's in-memory snapshots of CMS-owned state fresh: the language
/// directory (routable codes + default language), the maintenance-mode flag and
/// the www-redirect switch.
/// Program.cs performs one explicit refresh at startup so the very first request
/// already has real data; this service re-refreshes periodically after that, so a
/// language change or a maintenance toggle in the CMS takes effect within
/// <see cref="RefreshInterval"/> without restarting the Web app.
/// </summary>
internal sealed class SiteStateRefreshHostedService(
    ILanguageDirectory languageDirectory,
    IMaintenanceState maintenanceState,
    IWwwRedirectState wwwRedirectState,
    ILogger<SiteStateRefreshHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        do
        {
            try
            {
                // Every refresh is attempted regardless of the other's outcome — one
                // stale snapshot is no reason to let the second go stale too.
                Result languages = await languageDirectory.RefreshAsync(stoppingToken);
                if (languages.IsFailure)
                    logger.LogWarning("{Error} Keeping the previous snapshot.", languages.Error.Description);

                Result maintenance = await maintenanceState.RefreshAsync(stoppingToken);
                if (maintenance.IsFailure)
                    logger.LogWarning("{Error} Keeping the previous value.", maintenance.Error.Description);

                Result wwwRedirect = await wwwRedirectState.RefreshAsync(stoppingToken);
                if (wwwRedirect.IsFailure)
                    logger.LogWarning("{Error} Keeping the previous value.", wwwRedirect.Error.Description);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Backstop for anything the services do not classify as a refresh
                // failure — the loop must survive to try again either way.
                logger.LogWarning(ex, "Site state refresh threw unexpectedly; keeping previous snapshots.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
