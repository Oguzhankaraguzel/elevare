namespace Application.Features.Queries.SiteHealth.GetSiteHealth;

/// <summary>How loudly a <see cref="SiteHealthIssue"/> should be presented.</summary>
public enum SiteHealthSeverity
{
    /// <summary>
    /// Something an operator should know about, but which the site is surviving —
    /// a deliberately closed site, or a feature that silently degrades.
    /// </summary>
    Warning = 1,

    /// <summary>Something is broken and visitors or editors are feeling it.</summary>
    Error = 2,
}
