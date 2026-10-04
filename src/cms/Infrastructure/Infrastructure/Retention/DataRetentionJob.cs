using Application.Abstraction.Data;
using Application.Features.Commands.Trash;
using Domain.Entities.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Concrete;
using System.Globalization;

namespace Infrastructure.Retention;

/// <summary>
/// Hangfire job that trims append-only telemetry to its retention window.
/// Registered as the recurring job "data-retention" (nightly).
/// <para>
/// Deletes in bounded batches with <c>ExecuteDeleteAsync</c> — a set-based DELETE
/// that never loads rows into the change tracker, which matters because these
/// tables are the ones expected to reach millions of rows. Each table is trimmed
/// independently so one failure cannot stop the others.
/// </para>
/// </summary>
public sealed class DataRetentionJob(
    ICmsApplicationDbContext db,
    IOptions<RetentionOptions> options,
    ILogger<DataRetentionJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        RetentionOptions settings = options.Value;
        int analyticsDays = Math.Max(settings.AnalyticsDays, RetentionOptions.MinimumRetentionDays);
        int logDays = Math.Max(settings.LogDays, RetentionOptions.MinimumRetentionDays);
        int batchSize = Math.Clamp(settings.BatchSize, 100, 50_000);

        DateTime analyticsCutoff = DateTime.UtcNow.AddDays(-analyticsDays);
        DateTime logCutoff = DateTime.UtcNow.AddDays(-logDays);

        logger.LogInformation(
            "Data retention started. Analytics older than {AnalyticsCutoff:u} and logs older than {LogCutoff:u} will be removed.",
            analyticsCutoff, logCutoff);

        Result<int> views = await TrimAsync("PageViewHits",
            () => db.PageViewHits.Where(h => h.ViewedAtUtc < analyticsCutoff).OrderBy(h => h.Id).Take(batchSize),
            cancellationToken);

        Result<int> clicks = await TrimAsync("PageClickHits",
            () => db.PageClickHits.Where(h => h.ClickedAtUtc < analyticsCutoff).OrderBy(h => h.Id).Take(batchSize),
            cancellationToken);

        Result<int> logs = await TrimAsync("AppLogs",
            () => db.AppLogs.Where(l => l.CreatedAtUtc < logCutoff).OrderBy(l => l.Id).Take(batchSize),
            cancellationToken);

        int trashPurged = await TrimTrashAsync(settings, cancellationToken);

        logger.LogInformation(
            "Data retention finished. Removed {Views} page view(s), {Clicks} click(s), {Logs} log entry/entries, {Trash} trashed item(s).",
            Describe(views), Describe(clicks), Describe(logs), trashPurged);

        // Every table is attempted before anything is thrown — one locked table is no
        // reason to leave the other two growing. But the job does NOT report success
        // afterwards: a retention job that quietly fails every night is how a disk
        // fills up, so the failure is raised for Hangfire to record, retry and show on
        // the dashboard. Throwing IS the reporting channel for a job entry point.
        string[] failures =
        [
            .. new[] { views, clicks, logs }
                .Where(r => r.IsFailure)
                .Select(r => r.Error.Description)
        ];

        if (failures.Length > 0)
            throw new InvalidOperationException(
                $"Data retention could not trim {failures.Length} table(s): {string.Join(" | ", failures)}");
    }

    /// <summary>
    /// Destroys trashed pages that have sat past their window. Goes through
    /// <see cref="TrashPurge"/> one page at a time rather than a set-based DELETE:
    /// the safety checks are the whole point here. A page still holding form
    /// submissions is left in the Trash forever rather than having a timer decide
    /// that visitor enquiries have expired.
    /// <para>
    /// TrashDays = 0 means "keep the Trash indefinitely", the behaviour before this
    /// existed, so upgrading cannot delete anything a site did not ask it to.
    /// </para>
    /// </summary>
    private async Task<int> TrimTrashAsync(RetentionOptions settings, CancellationToken cancellationToken)
    {
        if (settings.TrashDays <= 0)
            return 0;

        int trashDays = Math.Max(settings.TrashDays, RetentionOptions.MinimumRetentionDays);
        DateTime cutoff = DateTime.UtcNow.AddDays(-trashDays);

        List<int> expired = await db.PageInfos
            .IgnoreQueryFilters()
            .Where(p => p.IsDeleted && p.DeleteDate != null && p.DeleteDate < cutoff)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        int purged = 0;
        foreach (int pageId in expired)
        {
            Result result = await TrashPurge.PurgePageAsync(db, pageId, cancellationToken);
            if (result.IsSuccess)
                purged++;
        }

        if (purged > 0)
            await db.SaveChangesAsync(cancellationToken);

        return purged;
    }

    private static string Describe(Result<int> trimmed) =>
        trimmed.IsSuccess ? trimmed.Value.ToString(CultureInfo.InvariantCulture) : "0 (failed)";

    /// <summary>
    /// Deletes everything the query selects, one batch at a time. The query is a
    /// factory because each pass must be re-evaluated against the shrinking table.
    /// <para>
    /// Returns how many rows went, or the reason it stopped. The count is kept even on
    /// failure paths in the log message, because "deleted 40 000 rows then hit a lock
    /// timeout" and "could not delete anything" call for different responses.
    /// </para>
    /// </summary>
    private async Task<Result<int>> TrimAsync<T>(
        string tableName, Func<IQueryable<T>> batchQuery, CancellationToken cancellationToken)
        where T : class
    {
        int total = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int deleted = await batchQuery().ExecuteDeleteAsync(cancellationToken);
                total += deleted;
                if (deleted == 0)
                    break;
            }

            return Result.Success(total);
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or TimeoutException)
        {
            logger.LogError(ex, "Data retention failed while trimming {Table} after {Deleted} row(s).", tableName, total);
            return Result.Failure<int>(RetentionErrors.TrimFailed(tableName, total, ex.Message));
        }
    }
}
