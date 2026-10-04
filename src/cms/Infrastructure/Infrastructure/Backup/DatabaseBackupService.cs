using System.Diagnostics;
using System.Globalization;
using Application.Abstraction.Services.Backup;
using Domain.Entities.Backups;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SharedKernel.Concrete;

namespace Infrastructure.Backup;

/// <summary>
/// <see cref="IDatabaseBackupService"/> over <c>pg_dump</c>/<c>pg_restore</c>, shelled
/// out to as external processes rather than run through EF or a driver call.
/// <para>
/// Unlike SQL Server, PostgreSQL has no in-database <c>BACKUP DATABASE</c> statement
/// — a consistent, restorable dump is only ever produced by running the
/// <c>pg_dump</c> client tool against the server, so this class's job is to invoke
/// it correctly and manage the files it produces (list, verify, prune). The runtime
/// image installs <c>postgresql-client</c> for exactly this (see the CMS Dockerfile).
/// </para>
/// <para>
/// Custom format (<c>pg_dump -Fc</c>) is used rather than plain SQL: it is already
/// compressed, and it is the only format <c>pg_restore --list</c> can sanity-check
/// without actually restoring anything.
/// </para>
/// </summary>
internal sealed class DatabaseBackupService(
    IConfiguration configuration,
    IOptions<BackupOptions> options,
    ILogger<DatabaseBackupService> logger) : IDatabaseBackupService
{
    private const string BackupExtension = "dump";
    private const string DurationExtension = "meta";

    public async Task<Result<string>> CreateAsync(CancellationToken cancellationToken = default)
    {
        Result<BackupConnectionInfo> connection = ResolveConnection();
        if (connection.IsFailure)
            return Result.Failure<string>(connection.Error);

        BackupOptions settings = options.Value;
        BackupConnectionInfo info = connection.Value;

        try
        {
            string directory = ResolveDirectory(settings);
            Directory.CreateDirectory(directory);

            string baseName = string.Create(
                CultureInfo.InvariantCulture,
                $"{info.DatabaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            string filePath = Path.Combine(directory, $"{baseName}.{BackupExtension}");

            var stopwatch = Stopwatch.StartNew();

            // -F c: custom (compressed, seekable) format — the only one pg_restore
            // --list can validate without a live scratch database to restore into.
            string[] dumpArgs =
            [
                "-h", info.Host, "-p", info.Port.ToString(CultureInfo.InvariantCulture),
                "-U", info.Username, "-F", "c", "-f", filePath, info.DatabaseName,
            ];
            Result dumpResult = await RunPgToolAsync("pg_dump", dumpArgs, info.Password, settings, cancellationToken);
            if (dumpResult.IsFailure)
            {
                logger.LogError("Database backup failed: {Error}", dumpResult.Error.Description);
                return Result.Failure<string>(dumpResult.Error);
            }

            stopwatch.Stop();

            if (settings.VerifyAfterBackup)
            {
                // pg_restore has no server-side "verify" the way RESTORE VERIFYONLY
                // does — --list is the closest equivalent: it reads the archive's own
                // table of contents, which fails immediately on a truncated or
                // corrupted file without touching any database.
                Result verifyResult = await RunPgToolAsync(
                    "pg_restore", ["--list", filePath], password: null, settings, cancellationToken);
                if (verifyResult.IsFailure)
                {
                    logger.LogError("Database backup verification failed: {Error}", verifyResult.Error.Description);
                    return Result.Failure<string>(verifyResult.Error);
                }
            }

            await File.WriteAllTextAsync(
                Path.Combine(directory, $"{baseName}.{DurationExtension}"),
                ((int)stopwatch.Elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture),
                cancellationToken);

            logger.LogInformation("Database backup written to {FilePath}.", filePath);
            return Result.Success(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Database backup failed.");
            return Result.Failure<string>(BackupErrors.Failed(ex.Message));
        }
    }

    public Task<Result<List<DatabaseBackupInfo>>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        Result<BackupConnectionInfo> connection = ResolveConnection();
        if (connection.IsFailure)
            return Task.FromResult(Result.Failure<List<DatabaseBackupInfo>>(connection.Error));

        try
        {
            string directory = ResolveDirectory(options.Value);
            if (!Directory.Exists(directory))
                return Task.FromResult(Result.Success(new List<DatabaseBackupInfo>()));

            // The files themselves are the source of truth, same as before — only the
            // source of the "how it was listed" changed (SQL Server's own DMV enumerated
            // its backup folder from inside the server; here the app just reads the
            // directory it wrote them to itself). Duration comes from the sidecar
            // .meta file CreateAsync writes next to each backup, if one exists — older
            // backups taken before this existed simply show no duration, same as the
            // SQL Server version already did whenever its own history join found nothing.
            List<DatabaseBackupInfo> backups = [.. new DirectoryInfo(directory)
                .EnumerateFiles($"*.{BackupExtension}")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => new DatabaseBackupInfo(
                    FilePath: f.FullName,
                    FileName: f.Name,
                    CreatedAtUtc: f.LastWriteTimeUtc,
                    SizeBytes: f.Length,
                    DurationSeconds: ReadDuration(directory, f.Name)))];

            return Task.FromResult(Result.Success(backups));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Listing database backups failed.");
            return Task.FromResult(Result.Failure<List<DatabaseBackupInfo>>(BackupErrors.Failed(ex.Message)));
        }
    }

    public async Task<Result<int>> PruneAsync(CancellationToken cancellationToken = default)
    {
        int keepCount = Math.Max(options.Value.KeepCount, BackupOptions.MinimumKeepCount);

        Result<List<DatabaseBackupInfo>> existing = await GetBackupsAsync(cancellationToken);
        if (existing.IsFailure)
            return Result.Failure<int>(existing.Error);

        // GetBackupsAsync returns newest-first, so everything past the keep count is
        // expired, and each file is deleted BY PATH — same reasoning as the SQL
        // Server version: a cutoff-date comparison is one timezone bug away from
        // silently keeping everything forever, while deleting a named path either
        // works or fails loudly.
        List<DatabaseBackupInfo> expired = [.. existing.Value.Skip(keepCount)];
        if (expired.Count == 0)
            return Result.Success(0);

        try
        {
            int prunedCount = 0;
            foreach (DatabaseBackupInfo backup in expired)
            {
                File.Delete(backup.FilePath);

                string metaPath = Path.ChangeExtension(backup.FilePath, DurationExtension);
                if (File.Exists(metaPath))
                    File.Delete(metaPath);

                prunedCount++;
            }

            logger.LogInformation(
                "Pruned {Count} backup file(s), keeping the newest {Keep}.", prunedCount, keepCount);

            return Result.Success(prunedCount);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A failed prune must not fail the backup that preceded it — a disk that
            // fills up later is a smaller problem than no backup at all today.
            logger.LogError(ex, "Pruning old database backups failed.");
            return Result.Failure<int>(BackupErrors.Failed(ex.Message));
        }
    }

    private static int? ReadDuration(string directory, string backupFileName)
    {
        string metaPath = Path.Combine(
            directory, $"{Path.GetFileNameWithoutExtension(backupFileName)}.{DurationExtension}");

        return File.Exists(metaPath)
            && int.TryParse(File.ReadAllText(metaPath), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
            ? seconds
            : null;
    }

    /// <summary>
    /// Runs a PostgreSQL client tool (<c>pg_dump</c>/<c>pg_restore</c>) as a child
    /// process. The password is passed via the <c>PGPASSWORD</c> environment
    /// variable rather than a command-line argument or a <c>.pgpass</c> file — the
    /// former would leak it into the process list, the latter needs a file on disk.
    /// </summary>
    private static async Task<Result> RunPgToolAsync(
        string tool, string[] args, string? password, BackupOptions settings, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(tool)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (string arg in args)
            startInfo.ArgumentList.Add(arg);

        if (password is not null)
            startInfo.Environment["PGPASSWORD"] = password;

        using Process process = new() { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Result.Failure(BackupErrors.Failed(
                $"Could not start {tool} — is postgresql-client installed? ({ex.Message})"));
        }

        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(
            Math.Clamp(settings.TimeoutMinutes, 1, 24 * 60)));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { /* already exited */ }
            return Result.Failure(BackupErrors.Failed($"{tool} timed out after {settings.TimeoutMinutes} minute(s)."));
        }

        string stderr = await stderrTask;
        await stdoutTask;

        if (process.ExitCode == 0)
            return Result.Success();

        string detail = string.IsNullOrWhiteSpace(stderr) ? $"{tool} exited with code {process.ExitCode}." : stderr.Trim();
        return Result.Failure(BackupErrors.Failed(detail));
    }

    /// <summary>
    /// The configured directory, or an app-owned default under the process's own
    /// base directory. Postgres, unlike SQL Server, owns no server-side folder this
    /// app could ask for and inherit permissions on — so unlike the old
    /// implementation, "leave it empty" now means "app-chosen", not "server-chosen".
    /// </summary>
    private static string ResolveDirectory(BackupOptions settings) =>
        string.IsNullOrWhiteSpace(settings.Directory)
            ? Path.Combine(AppContext.BaseDirectory, "Backups")
            : settings.Directory.TrimEnd('\\', '/');

    private Result<BackupConnectionInfo> ResolveConnection()
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            return Result.Failure<BackupConnectionInfo>(BackupErrors.ConnectionStringMissing);

        NpgsqlConnectionStringBuilder builder = new(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
            return Result.Failure<BackupConnectionInfo>(BackupErrors.ConnectionStringMissing);

        return Result.Success(new BackupConnectionInfo(
            builder.Host ?? "localhost", builder.Username ?? "", builder.Port, builder.Password ?? "", builder.Database));
    }
}
