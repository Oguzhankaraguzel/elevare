using Application.Abstraction.Services;

namespace WebMvc.Routing;

/// <summary>
/// Reads the language a request was browsing in straight from its path.
/// <para>
/// The middleware pipeline needs this before — or instead of — routing: the
/// exception handler runs when routing has already failed, and the maintenance
/// middleware runs before routing happens at all. Both still have to answer
/// "which language was the visitor in?" to pick the right system page, so they
/// apply the same rule the route constraint does (see
/// <see cref="ExistingLanguageRouteConstraint"/>): a first path segment that
/// names a known active language IS the language prefix.
/// </para>
/// </summary>
public static class RequestLanguage
{
    /// <summary>
    /// Returns the language code prefixing the request path, or the site's default
    /// when the path carries no prefix (which is exactly how default-language URLs
    /// are written — see PageInfo.ComputeFullSlug on the CMS side).
    /// </summary>
    public static string Resolve(HttpContext context, ILanguageDirectory languageDirectory)
    {
        string path = context.Request.Path.Value ?? string.Empty;
        string firstSegment = path.Trim('/').Split('/', 2)[0];

        return firstSegment.Length > 0 && languageDirectory.IsKnownLanguageCode(firstSegment)
            ? firstSegment
            : languageDirectory.DefaultLanguageCode;
    }
}
