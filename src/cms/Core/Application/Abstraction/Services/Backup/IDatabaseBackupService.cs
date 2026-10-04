using SharedKernel.Concrete;

namespace Application.Abstraction.Services.Backup;

/// <summary>
/// Takes and lists PostgreSQL database backups.
/// <para>
/// Every operation shells out to the <c>pg_dump</c>/<c>pg_restore</c> client
/// tools rather than running through EF or a driver call — PostgreSQL, unlike SQL
/// Server, has no in-database <c>BACKUP DATABASE</c> statement, so a consistent
/// dump only ever comes from running the client tool against the server. The
/// files themselves are then this app's own responsibility to list and prune,
/// since (also unlike SQL Server) there is no server-owned backup folder to
/// delegate that to.
/// </para>
/// <para>
/// <c>pg_restore</c>'s actual restore mode is intentionally absent. Restoring
/// overwrites the live database; a button that does that is worth less than the
/// risk it carries, so it stays a deliberate manual operation (the command is
/// documented on the About page).
/// </para>
/// </summary>
public interface IDatabaseBackupService
{
    /// <summary>
    /// Takes a full backup and verifies it is readable. Returns the resulting file path.
    /// </summary>
    Task<Result<string>> CreateAsync(CancellationToken cancellationToken = default);

    /// <summary>Completed backups, newest first.</summary>
    Task<Result<List<DatabaseBackupInfo>>> GetBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes backup files older than the configured retention count. Returns how
    /// many files were removed.
    /// </summary>
    Task<Result<int>> PruneAsync(CancellationToken cancellationToken = default);
}
