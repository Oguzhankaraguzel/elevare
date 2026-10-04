namespace Domain.Entities.PublicLogs;

/// <summary>
/// Mirrors the CMS's <c>Domain.Entities.Logs.AppLogLevel</c> numeric values.
/// Kept as an independent copy so the public Web app has no compile-time
/// dependency on the CMS module — see <c>PublicPageStatus</c> for the same
/// reasoning on the read side.
/// </summary>
public enum PublicAppLogLevel
{
    Trace = 1,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}
