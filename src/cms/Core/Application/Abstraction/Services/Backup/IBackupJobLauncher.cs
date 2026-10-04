using SharedKernel.Concrete;

namespace Application.Abstraction.Services.Backup;

/// <summary>
/// Starts a one-off backup in the background.
/// <para>
/// The "Back up now" button does NOT wait for the backup to finish: a full backup
/// runs for minutes, and holding a Blazor circuit open that long would look like a
/// hung page. The button hands the work to the job scheduler and returns; the
/// operator refreshes the list to see the result.
/// </para>
/// </summary>
public interface IBackupJobLauncher
{
    /// <summary>Queues a backup. Returns the scheduler's job id.</summary>
    Result<string> EnqueueBackup();
}
