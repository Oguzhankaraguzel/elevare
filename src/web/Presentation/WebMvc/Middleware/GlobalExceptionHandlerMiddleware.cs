using Application.Exceptions;
using Application.Services;
using Domain.Entities.PublicLogs;

namespace WebMvc.Middleware;

/// <summary>
/// Catches unhandled exceptions and maps them to appropriate HTTP responses.
/// <list type="bullet">
///   <item><see cref="NotFoundException"/> → 404 error page (a missing page itself
///   does not throw — see <c>PageController</c>)</item>
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
            // A missing page is an answer, not a fault: one line, no stack trace.
#pragma warning disable S6667
            logger.LogInformation("Not found: {Path} ({Reason})", context.Request.Path, ex.Message);
#pragma warning restore S6667
            await ErrorPages.RecordAsync(context, PublicAppLogLevel.Warning, ex.Message, exception: null);

            await ErrorPages.RenderNotFoundAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred");
            await ErrorPages.RecordAsync(context, PublicAppLogLevel.Error, ex.Message, ex.ToString());

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();

                if (environment.IsDevelopment())
                {
                    // In development, let the developer exception page handle it
                    throw;
                }

                await ErrorPages.RenderServerErrorAsync(context);
            }
        }
    }
}
