using Application.Abstraction.Services.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Infrastructure.Authentication;

/// <summary>
/// Fills the <see cref="IRolePermissionCache"/> snapshot once at startup so the
/// very first authorised render already has real data. A failure here is logged
/// and swallowed: an empty snapshot denies permissions (fail-closed) rather than
/// preventing the CMS from starting, and the next role edit repopulates it.
/// </summary>
internal sealed class RolePermissionCacheWarmup(
    IRolePermissionCache cache,
    ILogger<RolePermissionCacheWarmup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Result warmed = await cache.RefreshAsync(cancellationToken);
        if (warmed.IsFailure)
            logger.LogError(
                "Could not warm the role permission cache at startup — permissions will read as denied until a role is saved or the app restarts. {Error}",
                warmed.Error.Description);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
