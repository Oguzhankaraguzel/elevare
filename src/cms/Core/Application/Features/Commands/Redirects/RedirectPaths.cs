namespace Application.Features.Commands.Redirects;

/// <summary>
/// The one place redirect paths are cleaned up and compared. Hand-typed rules arrive
/// in every shape a person might type — <c>/eski-sayfa</c>, <c>eski-sayfa/</c>,
/// <c>  /Eski-Sayfa  </c> — while the rules the CMS writes itself are always bare
/// FullSlugs. Without a single normalisation step the uniqueness check misses
/// duplicates and the loop check misses loops.
/// </summary>
public static class RedirectPaths
{
    /// <summary>
    /// Storage form for the path being redirected FROM: no leading or trailing
    /// slash, matching how <c>PageInfo.FullSlug</c> is stored, because that is what
    /// the public site compares an incoming request against.
    /// </summary>
    public static string NormalizeOld(string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Trim('/');

    /// <summary>
    /// Storage form for the target. An external URL is left exactly as typed — its
    /// slashes are not ours to touch — and an internal path is given a single
    /// leading slash so the browser is sent somewhere absolute rather than to a
    /// path relative to wherever the visitor happened to be.
    /// </summary>
    public static string? NormalizeNew(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string trimmed = path.Trim();
        return IsExternal(trimmed) ? trimmed : "/" + trimmed.Trim('/');
    }

    /// <summary>Comparison form: both sides stripped of slashes so shapes stop mattering.</summary>
    public static string ForComparison(string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Trim('/');

    public static bool IsExternal(string path) =>
        path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("//", StringComparison.Ordinal);
}
