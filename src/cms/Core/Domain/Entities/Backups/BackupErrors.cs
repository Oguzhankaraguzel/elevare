using SharedKernel.Concrete;

namespace Domain.Entities.Backups;

public static class BackupErrors
{
    public static readonly Error ConnectionStringMissing = Error.Failure(
        "Backup.ConnectionStringMissing",
        "No database connection string is configured, so no backup can be taken.");

    /// <summary>
    /// Carries pg_dump/pg_restore's own stderr text, because the operator
    /// genuinely needs it: the common failures (permission denied on a custom
    /// folder, out of disk space, a wrong password, the client tool missing from
    /// the image) are only distinguishable from that text.
    /// </summary>
    public static Error Failed(string detail) => Error.Failure("Backup.Failed", detail);
}
