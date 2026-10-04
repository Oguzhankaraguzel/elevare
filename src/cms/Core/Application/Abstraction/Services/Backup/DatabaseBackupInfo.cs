namespace Application.Abstraction.Services.Backup;

/// <summary>
/// One database backup file present on the database server.
/// <para>
/// Sourced from the actual filesystem (enumerated BY SQL Server, see
/// <see cref="IDatabaseBackupService"/>) rather than from SQL Server's backup
/// history, so the list can never show a restore point that no longer exists —
/// a stale history row is exactly the kind of thing you discover at the worst
/// possible moment.
/// </para>
/// </summary>
/// <param name="FilePath">Full path of the backup file on the database server.</param>
/// <param name="FileName">Just the file name, for display.</param>
/// <param name="CreatedAtUtc">The file's last-write time, converted to UTC.</param>
/// <param name="SizeBytes">Size of the file on disk.</param>
/// <param name="DurationSeconds">
/// How long the backup took, from SQL Server's history. Null when the history row
/// has aged out — the file is still perfectly restorable, only the timing is lost.
/// </param>
public sealed record DatabaseBackupInfo(
    string FilePath,
    string FileName,
    DateTime CreatedAtUtc,
    long SizeBytes,
    int? DurationSeconds);
