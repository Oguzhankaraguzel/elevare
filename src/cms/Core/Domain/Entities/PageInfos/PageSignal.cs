namespace Domain.Entities.PageInfos;

/// <summary>
/// Everything notable about a page's current state, as one bit field.
/// <para>
/// One definition serves both jobs: the page list shows these as badges, and the
/// filters ask for them by the same name. Splitting "what to display" from "what to
/// filter by" is how the two drift — a filter for pages missing structured data that
/// disagrees with the badge saying structured data is missing helps nobody.
/// </para>
/// <para>
/// Purely derived. Nothing here is stored; it is recomputed from the page's own
/// columns each time the list is built.
/// </para>
/// </summary>
[Flags]
public enum PageSignal
{
    None = 0,

    // ── Things a page is missing ──────────────────────────────────────────────
    MissingMetaDescription = 1 << 0,
    MissingStructuredData = 1 << 1,
    MissingOgImage = 1 << 2,
    MissingFocusKeyword = 1 << 3,

    /// <summary>Canonical is switched off, so this page points search engines elsewhere.</summary>
    NotCanonical = 1 << 4,

    /// <summary>Deliberately excluded from search results.</summary>
    NoIndex = 1 << 5,

    /// <summary>At least one active language has no version of this page.</summary>
    MissingTranslation = 1 << 6,

    /// <summary>The analyzer's last score was below <see cref="PageSignalRules.LowSeoScoreBelow"/>.</summary>
    LowSeoScore = 1 << 7,

    /// <summary>Untouched for longer than <see cref="PageSignalRules.StaleAfterDays"/> days.</summary>
    Stale = 1 << 8,

    // ── Workflow state ────────────────────────────────────────────────────────
    /// <summary>Sitting in an approval chain, waiting on somebody's decision.</summary>
    AwaitingApproval = 1 << 9,

    /// <summary>
    /// Live content is one thing, and a newer version is staged behind approval.
    /// This is the "published, but the update has not landed yet" case — the one
    /// state the old list could not show at all.
    /// </summary>
    HasStagedUpdate = 1 << 10,
}
