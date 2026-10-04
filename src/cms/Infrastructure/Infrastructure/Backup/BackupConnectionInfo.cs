namespace Infrastructure.Backup;

/// <summary>
/// The pieces of the configured connection string <c>pg_dump</c>/<c>pg_restore</c>
/// need as separate command-line arguments — taken from the connection string
/// rather than from configuration directly, so they can never disagree with what
/// EF itself connects to.
/// </summary>
internal sealed record BackupConnectionInfo(string Host, string Username, int Port, string Password, string DatabaseName);
