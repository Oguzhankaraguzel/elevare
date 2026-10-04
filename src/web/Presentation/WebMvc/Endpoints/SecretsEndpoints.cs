using Application.Abstraction.Services;
using Infrastructure.Caching;
using Microsoft.Extensions.Options;
using SharedKernel.Concrete;
using WebMvc.RateLimiting;

namespace WebMvc.Endpoints;

/// <summary>
/// Lets the CMS tell this app "a Captcha:* secret just changed, re-read it" —
/// called by <c>IntegrationSecretsChangeNotifier</c> (CMS Infrastructure) right
/// after a Sırlar save commits. Reuses <c>Cache:ClearSecret</c> for
/// authorization rather than adding a third shared secret for what is, at bottom,
/// the same "cms↔web internal admin call" concept <c>CacheEndpoints</c> already
/// established.
/// </summary>
public static class SecretsEndpoints
{
    public static WebApplication MapSecretsEndpoints(this WebApplication app)
    {
        app.MapPost("/api/secrets/reload", async (
            HttpContext http, IIntegrationSecretsReloader reloader, IOptions<CacheOptions> options, CancellationToken ct) =>
        {
            if (!InternalAdminAuthorization.IsAuthorized(http, options.Value.ClearSecret))
                return Results.Unauthorized();

            Result result = await reloader.ReloadAsync(ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status500InternalServerError);
        }).RequireRateLimiting(RateLimitPolicies.CacheAdmin);

        return app;
    }
}
