using Application.Abstraction.Services;
using Application.Exceptions;
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
/// Catches unhandled exceptions and maps them to appropriate HTTP responses.
/// <list type="bullet">
///   <item><see cref="NotFoundException"/> → 404 error page</item>
///   <item><see cref="OperationCanceledException"/> → silently ignored (no log, no response body)</item>
///   <item>All other exceptions → 500 error page</item>
/// </list>
/// <para>
/// Both error pages are CMS-editable: when a published builder page exists with the
/// reserved slug <c>404</c> / <c>500</c> (see <see cref="SystemPageProvider"/>), its
/// content replaces the built-in text; otherwise the static fallback is shown.
/// </para>
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlerMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or request was cancelled – do nothing.
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "Resource not found: {Message}", ex.Message);
            await TryPersistLogAsync(context, PublicAppLogLevel.Warning, ex);

            await RenderErrorPageAsync(
                context,
                StatusCodes.Status404NotFound,
                "~/Views/Shared/NotFound.cshtml",
                SystemPageProvider.NotFoundSlug);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred");
            await TryPersistLogAsync(context, PublicAppLogLevel.Error, ex);

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();

                if (environment.IsDevelopment())
                {
                    // In development, let the developer exception page handle it
                    throw;
                }

                await RenderErrorPageAsync(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "~/Views/Shared/Error.cshtml",
                    SystemPageProvider.ServerErrorSlug);
            }
        }
    }

    /// <summary>
    /// Persists the exception to the CMS-visible log table. Best-effort: a failure
    /// here (e.g. the DB itself is what broke) must never mask the original error.
    /// </summary>
    private static async Task TryPersistLogAsync(HttpContext context, PublicAppLogLevel level, Exception ex)
    {
        try
        {
            ISender sender = context.RequestServices.GetRequiredService<ISender>();
            await sender.Send(new RecordAppLogCommand(
                level,
                ex.Message,
                ex.ToString(),
                PublicAppLogSource.Server,
                context.Request.Path,
                context.Request.Headers.UserAgent.ToString()));
        }
        catch (Exception)
        {
            // Logging must never throw on top of the original exception.
        }
    }

    private static async Task RenderErrorPageAsync(
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
            // Resolved from the request rather than the primary constructor: this
            // method is static, and keeping it that way makes it obvious it holds no
            // per-request state of its own.
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
