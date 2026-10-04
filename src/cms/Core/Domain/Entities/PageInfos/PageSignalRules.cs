namespace Domain.Entities.PageInfos;

/// <summary>
/// The thresholds behind the judgement calls in <see cref="PageSignal"/>. Named
/// constants rather than numbers buried in a query, so "why is this page marked
/// stale?" has an answer that is not "read the LINQ".
/// </summary>
public static class PageSignalRules
{
    /// <summary>
    /// A page nobody has touched in three months. Long enough that a quarterly
    /// content review would have caught it, short enough to still be actionable.
    /// </summary>
    public const int StaleAfterDays = 90;

    /// <summary>
    /// Below this, the analyzer found enough wrong that the page is worth revisiting.
    /// Pages that have never been scored are not flagged — no score is not a bad score.
    /// </summary>
    public const int LowSeoScoreBelow = 50;
}
