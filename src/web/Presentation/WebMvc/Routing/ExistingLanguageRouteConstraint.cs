using Application.Abstraction.Services;

namespace WebMvc.Routing;

/// <summary>
/// Route constraint that validates whether a route segment matches a known language
/// code. Backed by <see cref="ILanguageDirectory"/> — an in-memory snapshot of the
/// CMS's Languages table — rather than a hardcoded list, so adding/removing a
/// language in the CMS takes effect here without a Web app restart.
/// </summary>
public sealed class ExistingLanguageRouteConstraint(ILanguageDirectory languageDirectory) : IRouteConstraint
{
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out object? value) || value is not string languageCode)
            return false;

        return languageDirectory.IsKnownLanguageCode(languageCode);
    }
}
