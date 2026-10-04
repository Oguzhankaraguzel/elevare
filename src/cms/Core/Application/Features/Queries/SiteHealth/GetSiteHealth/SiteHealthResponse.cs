namespace Application.Features.Queries.SiteHealth.GetSiteHealth;

/// <summary>
/// Snapshot of everything currently worth flagging about the site, newest concern
/// first. An empty list is the healthy state — the header indicator hides itself
/// entirely rather than showing a reassuring green badge, so the icon appearing at
/// all is the signal.
/// </summary>
public sealed record SiteHealthResponse(IReadOnlyList<SiteHealthIssue> Issues)
{
    public bool HasIssues => Issues.Count > 0;

    /// <summary>
    /// Worst severity present, used to colour the single header icon. Null when healthy.
    /// </summary>
    public SiteHealthSeverity? WorstSeverity =>
        Issues.Count == 0 ? null : Issues.Max(i => i.Severity);
}
