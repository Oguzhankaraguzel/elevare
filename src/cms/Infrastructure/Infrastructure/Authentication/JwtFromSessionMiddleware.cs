using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Authentication;

/// <summary>
/// Reads JWT stored in the user's session and injects it into the Authorization header
/// for downstream authentication handlers. Implemented as IMiddleware so it can be registered
/// and ordered in the pipeline.
/// </summary>
public sealed class JwtFromSessionMiddleware(ILogger<JwtFromSessionMiddleware> logger) : IMiddleware
{
    private const string SessionKey = "Jwt";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            if (context.Session is not null)
            {
                string? token = context.Session.GetString(SessionKey);
                if (!string.IsNullOrWhiteSpace(token) && !context.Request.Headers.ContainsKey("Authorization"))
                {
                    context.Request.Headers["Authorization"] = "Bearer " + token;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read JWT from session");
        }

        await next(context);
    }
}
