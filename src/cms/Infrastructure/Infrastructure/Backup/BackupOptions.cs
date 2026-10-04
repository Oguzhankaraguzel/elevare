namespace Infrastructure.Backup;

/// <summary>
/// Database backup settings, bound from <c>appsettings.json → Backup</c>.
/// </summary>
public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>
    /// Where backup files are written.
    /// <para>
    /// Leave EMPTY to use an app-owned default folder next to the running process.
    /// Unlike SQL Server, PostgreSQL has no server-side folder this app could
    /// inherit permissions on — <c>pg_dump</c> runs as this app's own process and
    /// writes wherever it's told, so the default just needs to be writable by
    /// whatever account runs the CMS.
    /// </para>
    /// <para>
    /// In Docker Compose this is set explicitly (<c>Backup__Directory</c>) to a
    /// path backed by its own named volume, independent from the database's own
    /// data volume — so a backup survives even if the live database doesn't.
    /// </para>
    /// </summary>
    public string Directory { get; init; } = "";

    /// <summary>
    /// How many backup files to keep. Older ones are deleted after each successful
    /// backup. Eight weekly backups ≈ two months of restore points.
    /// </summary>
    public int KeepCount { get; init; } = 8;

    /// <summary>
    /// Minutes allowed for one backup before the command times out. Backups are
    /// I/O-bound and can far exceed the default 30-second command timeout.
    /// </summary>
    public int TimeoutMinutes { get; init; } = 60;

    /// <summary>
    /// Whether to run <c>pg_restore --list</c> against the archive after each
    /// backup. Cheap compared to SQL Server's <c>RESTORE VERIFYONLY</c> (it reads
    /// the archive's own table of contents rather than the whole file) but is
    /// still the only thing that proves the file isn't truncated or corrupted —
    /// an unverified backup is a hope, not a backup.
    /// </summary>
    public bool VerifyAfterBackup { get; init; } = true;

    /// <summary>Never keep fewer than this many backups, whatever the settings say.</summary>
    public const int MinimumKeepCount = 2;
}
