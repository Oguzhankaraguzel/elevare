using Application.Abstraction.Services.Backup;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Infrastructure.Backup;

/// <summary>
/// Hangfire job that takes the weekly database backup and then trims old ones.
/// Registered as the recurring job "database-backup"; also enqueued as a one-off
/// by the "Back up now" button on <c>/admin/backups</c>.
/// </summary>
public sealed class DatabaseBackupJob(
    IDatabaseBackupService backupService,
    ILogger<DatabaseBackupJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        Result<string> backup = await backupService.CreateAsync(cancellationToken);
        if (backup.IsFailure)
        {
            // Thrown, not swallowed: a failed backup is the one job failure that must
            // be loud. Hangfire records it, retries it, and shows it on the dashboard.
            throw new InvalidOperationException(
                $"Database backup failed: {backup.Error.Description}");
        }

        // Pruning is best-effort by design — the backup already succeeded, and losing
        // it over a cleanup problem would be exactly backwards.
        Result<int> pruned = await backupService.PruneAsync(cancellationToken);
        if (pruned.IsFailure)
            logger.LogWarning("Backup succeeded but pruning old backups failed: {Error}", pruned.Error.Description);
    }
}
