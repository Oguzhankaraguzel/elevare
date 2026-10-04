using Application.Abstraction.Services.Backup;
using Domain.Entities.Backups;
using Hangfire;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Infrastructure.Backup;

internal sealed class HangfireBackupJobLauncher(ILogger<HangfireBackupJobLauncher> logger) : IBackupJobLauncher
{
    public Result<string> EnqueueBackup()
    {
        try
        {
            string jobId = BackgroundJob.Enqueue<DatabaseBackupJob>(
                job => job.ExecuteAsync(CancellationToken.None));

            logger.LogInformation("Manual database backup queued as job {JobId}.", jobId);
            return Result.Success(jobId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            // Hangfire throws InvalidOperationException when no job storage is
            // configured, and can time out if its SQL storage is unreachable.
            logger.LogError(ex, "Queueing a manual database backup failed.");
            return Result.Failure<string>(BackupErrors.Failed(ex.Message));
        }
    }
}
