using Application.Abstraction.Services;
using Application.Services;
using SharedKernel.Concrete;
using SharedKernel.Content;
using WebMvc.Routing;

namespace WebMvc.Middleware;

/// <summary>
/// When the CMS's <c>Advanced.MaintenanceModeEnabled</c> setting is on, every
/// request gets a 503 maintenance page instead of the site. The flag is read from
/// an in-memory snapshot (<see cref="IMaintenanceState"/>) refreshed every ~30s,
/// so toggling it in the CMS takes effect without restarting the Web app.
/// <para>
/// If a published page with the reserved slug <c>maintenance</c> exists, its
/// builder content is shown; otherwise the compiled-in default from
/// <see cref="MaintenancePageContent"/> is used — the same screen the CMS seeds,
/// so the two paths are visually identical. Placed after static files so the
/// page's background image still loads while the site is closed.
/// </para>
/// </summary>
public sealed class MaintenanceModeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IMaintenanceState maintenanceState,
        SystemPageProvider systemPages,
        ILanguageDirectory languageDirectory)
    {
        // /health answers about the process, not about the site being open. Letting
        // maintenance mode turn it into a 503 makes a container healthcheck restart a
        // perfectly healthy app, and an uptime monitor page someone at 3am, precisely
        // because an operator deliberately closed the site.
        if (!maintenanceState.IsEnabled || context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "3600";
        context.Response.ContentType = "text/html; charset=utf-8";

        // The visitor's language still matters while the site is down — someone who
        // was browsing /en/ should not suddenly be addressed in Turkish.
        string languageCode = RequestLanguage.Resolve(context, languageDirectory);

        // Never let a DB hiccup break the maintenance page itself: on failure the
        // built-in page is served. Inspecting the Result keeps that fallback explicit
        // instead of hiding it behind a bare catch.
        string html = MaintenancePageContent.BuildDocument(languageCode);
        Result<SystemPageContent?> custom = await systemPages.TryGetAsync(
            SystemPageProvider.MaintenanceSlug, languageCode, context.RequestAborted);

        if (custom.IsSuccess && custom.Value is not null)
            html = WrapCustomPage(custom.Value, languageCode);

        await context.Response.WriteAsync(html);
    }

    /// <summary>
    /// Renders a CMS-authored maintenance page as a standalone document. There is no
    /// site layout here on purpose — the point of maintenance mode is that the rest
    /// of the site is not being touched — so the reset the layout would normally
    /// supply is inlined instead, otherwise the browser's default body margin shows
    /// as a white border around a full-bleed design.
    /// </summary>
    private static string WrapCustomPage(SystemPageContent content, string languageCode) =>
        // Doubled '$' so the inlined CSS reset can use bare braces — a raw string
        // literal does not honour the usual "{{" escape.
        $$"""
        <!DOCTYPE html>
        <html lang="{{languageCode}}">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <meta name="robots" content="noindex" />
            <title>{{MaintenancePageContent.BrandName}}</title>
            <style>*,*::before,*::after{box-sizing:border-box}html,body{margin:0;padding:0;height:100%}
        {{content.Css}}</style>
        </head>
        <body>
        {{content.Html}}
        </body>
        </html>
        """;
}
