using Application.Abstraction.Services;
using Application.Features.Commands.Logs.RecordAppLog;
using Application.Services;
using Domain.Entities.PublicLogs;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SharedKernel.Concrete;
using WebMvc.Routing;

namespace WebMvc.Middleware;

/// <summary>
/// Writes the 404 and 500 pages and their log entries — shared by
/// <see cref="GlobalExceptionHandlerMiddleware"/> and <c>PageController</c>, which
/// answers a missing page itself rather than throwing for it.
/// </summary>
/// <remarks>
/// Both pages are CMS-editable: when a published builder page exists with the
/// reserved slug <c>404</c> / <c>500</c> (see <see cref="SystemPageProvider"/>), its
/// content replaces the built-in text; otherwise the static fallback is shown.
/// </remarks>
public static class ErrorPages
{
    public static Task RenderNotFoundAsync(HttpContext context) => RenderAsync(
        context, StatusCodes.Status404NotFound, "~/Views/Shared/NotFound.cshtml", SystemPageProvider.NotFoundSlug);

    public static Task RenderServerErrorAsync(HttpContext context) => RenderAsync(
        context, StatusCodes.Status500InternalServerError, "~/Views/Shared/Error.cshtml", SystemPageProvider.ServerErrorSlug);

    /// <summary>
    /// Persists an entry to the CMS-visible log table. Best-effort: a failure
    /// here (e.g. the DB itself is what broke) must never mask the original error.
    /// </summary>
    public static async Task RecordAsync(HttpContext context, PublicAppLogLevel level, string message, string? exception)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            ISender sender = context.RequestServices.GetRequiredService<ISender>();
            await sender.Send(new RecordAppLogCommand(
                level,
                message,
                exception,
                PublicAppLogSource.Server,
                context.Request.Path,
                context.Request.Headers.UserAgent.ToString()));
        }
        catch (Exception)
        {
            // Logging must never throw on top of the original exception.
        }
    }

    private static async Task RenderAsync(
        HttpContext context,
        int statusCode,
        string viewName,
        string systemPageSlug)
    {
        context.Response.StatusCode = statusCode;

        ViewDataDictionary viewData = new(
            context.RequestServices.GetRequiredService<IModelMetadataProvider>(),
            new ModelStateDictionary());

        // CMS-editable content: a published page with the reserved slug ("404"/"500"),
        // in the language the visitor was actually browsing — a dead link under /en/
        // must not answer with the Turkish error page.
        // A failure here (e.g. the DB itself is what broke) falls back to the static
        // view — this code path is already handling an error, so it must not become a
        // second one. The Result is inspected rather than caught so the fallback is a
        // decision, not an accident.
        SystemPageProvider systemPages = context.RequestServices.GetRequiredService<SystemPageProvider>();
        string languageCode = RequestLanguage.Resolve(
            context, context.RequestServices.GetRequiredService<ILanguageDirectory>());

        Result<SystemPageContent?> custom = await systemPages.TryGetAsync(
            systemPageSlug, languageCode, context.RequestAborted);

        if (custom.IsFailure)
        {
            context.RequestServices
                .GetRequiredService<ILogger<GlobalExceptionHandlerMiddleware>>()
                .LogWarning(
                    "Falling back to the built-in {Slug} page: {Error}", systemPageSlug, custom.Error.Description);
        }
        else if (custom.Value is not null)
        {
            viewData["SystemPageHtml"] = custom.Value.Html;
            viewData["SystemPageCss"] = custom.Value.Css;
            viewData[SiteCodeExclusions.ViewDataKey] = custom.Value.ExcludedSiteCodeIds;
        }

        var routeData = new RouteData();
        routeData.Values["controller"] = "Error";
        routeData.Values["action"] = statusCode == StatusCodes.Status404NotFound ? "NotFound" : "InternalServerError";

        var actionContext = new ActionContext(context, routeData, new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var result = new ViewResult
        {
            ViewName = viewName,
            StatusCode = statusCode,
            ViewData = viewData
        };
        await result.ExecuteResultAsync(actionContext);
    }
}
