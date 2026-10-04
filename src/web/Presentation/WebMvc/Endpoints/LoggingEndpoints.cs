using Application.Features.Commands.Logs.RecordAppLog;
using Domain.Entities.PublicLogs;
using MediatR;
using WebMvc.RateLimiting;

namespace WebMvc.Endpoints;

/// <summary>
/// Client-side error reporting endpoint, called by the <c>window.onerror</c>/
/// <c>unhandledrejection</c> handlers in elevare-interactions.js. Server-side
/// exceptions are logged separately by <see cref="Middleware.GlobalExceptionHandlerMiddleware"/>.
/// </summary>
public static class LoggingEndpoints
{
    public static WebApplication MapLoggingEndpoints(this WebApplication app)
    {
        app.MapPost("/api/log/client", async (LogClientErrorRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (TrackingEndpoints.IsBot(http) || string.IsNullOrWhiteSpace(request.Message))
                return Results.NoContent();

            await sender.Send(new RecordAppLogCommand(
                PublicAppLogLevel.Error,
                request.Message,
                request.Stack,
                PublicAppLogSource.Client,
                request.Path,
                http.Request.Headers.UserAgent.ToString()), ct);

            return Results.NoContent();
        }).RequireRateLimiting(RateLimitPolicies.ClientLog);

        return app;
    }
}
