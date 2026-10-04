namespace Infrastructure.Retention;

/// <summary>
/// How long append-only telemetry is kept, bound from <c>appsettings.json → Retention</c>.
/// <para>
/// Only tables that grow without bound and carry no business value past their
/// reporting window are covered: page views, click tracking and application logs.
/// Form submissions are deliberately NOT included — those are real customer
/// enquiries, not telemetry, and must never be deleted on a timer.
/// </para>
/// </summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Days of page-view/click telemetry to keep. Default 90 (~3 months).</summary>
    public int AnalyticsDays { get; init; } = 90;

    /// <summary>Days of application logs to keep. Default 90 (~3 months).</summary>
    public int LogDays { get; init; } = 90;

    /// <summary>
    /// Days a soft-deleted item stays in the Trash before it is destroyed for good.
    /// Default 30 — long enough to cover "I deleted the wrong thing and noticed after
    /// the weekend", short enough that the tables do not grow forever. Set to 0 to
    /// keep the Trash indefinitely, which is what the CMS did before this existed.
    /// <para>
    /// Pages holding form submissions or sub-pages are skipped by the purge, so this
    /// timer can never quietly take visitor data with it.
    /// </para>
    /// </summary>
    public int TrashDays { get; init; } = 30;

    /// <summary>
    /// Rows deleted per statement. Keeping this bounded stops a first run over a
    /// long-neglected table from taking one enormous lock; the job simply loops.
    /// </summary>
    public int BatchSize { get; init; } = 5000;

    /// <summary>Safety valve: never delete anything newer than this many days, whatever the settings say.</summary>
    public const int MinimumRetentionDays = 7;
}
