using Application.Abstraction.Services;
using Microsoft.AspNetCore.Mvc.Routing;

namespace WebMvc.Routing;

/// <summary>
/// Transforms incoming URL segments <c>/{languageCode}/{slug}</c> into controller/action route values.
/// <para>
/// This is the central point for multi-language slug resolution.
/// It reads the language code and slug from the URL, then forwards them
/// to the appropriate controller for content lookup.
/// </para>
/// </summary>
public sealed class SlugRouteValueTransformer(
    ILogger<SlugRouteValueTransformer> logger,
    ILanguageDirectory languageDirectory) : DynamicRouteValueTransformer
{
    private const string DefaultController = "Page";
    private const string DefaultAction = "Index";

    public override ValueTask<RouteValueDictionary> TransformAsync(
        HttpContext httpContext,
        RouteValueDictionary values)
    {
        // No language segment in the URL (bare-slug route) => the CMS's current
        // default language, not a hardcoded one.
        string languageCode = values["languageCode"]?.ToString() ?? languageDirectory.DefaultLanguageCode;
        string slug = values["slug"]?.ToString() ?? string.Empty;

        logger.LogDebug(
            "Resolving route: lang={LanguageCode}, slug={Slug}",
            languageCode, slug);

        // Store language code in route values so controllers can access it.
        var result = new RouteValueDictionary
        {
            ["controller"] = DefaultController,
            ["action"] = DefaultAction,
            ["languageCode"] = languageCode,
            ["slug"] = slug
        };

        return ValueTask.FromResult(result);
    }
}
