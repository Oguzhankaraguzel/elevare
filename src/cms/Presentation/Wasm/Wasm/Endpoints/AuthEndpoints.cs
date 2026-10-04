using System.IdentityModel.Tokens.Jwt;
using SharedKernel.Abstraction.Messaging;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using SharedKernel.Concrete;
using Application.Abstraction.Services;
using Application.Features.Auth.Login;
using Domain.Entities.Logs;
using Wasm.RateLimiting;

namespace Wasm.Endpoints;

public static class AuthEndpoints
{
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/account/login", async (
            HttpContext httpContext,
            ISender sender,
            ILoggerFactory loggerFactory,
            [FromForm] string identifier,
            [FromForm] string password,
            [FromForm] string? returnUrl) =>
        {
            LoginCommand cmd = new(identifier, password);
            Result<LoginResponse> r = await sender.Send(cmd);
            if (!r.IsSuccess)
            {
                // Distinguish a locked account from a wrong password. The login page
                // has always had a message for "locked"; it was unreachable because
                // every failure was reported as "invalid", so someone locked out kept
                // retrying against a wall with no idea why.
                // Anything else stays deliberately vague: which of the two an unknown
                // identifier hit is not a caller's business.
                string reason = r.Error.Code == AuthErrors.AccountLocked.Code ? "locked" : "invalid";
                return Results.Redirect($"/login?error={reason}");
            }

            // Store token in session
            try
            {
                httpContext.Session.SetString("Jwt", r.Value.Token);
            }
            catch (Exception ex)
            {
                // The sign-in itself succeeded; only stashing the token failed. Logged
                // rather than surfaced, because the redirect below still lands the user
                // somewhere sensible and a session write is not their problem to fix.
                loggerFactory.CreateLogger(typeof(AuthEndpoints))
                    .LogError(ex, "Could not store the session token after a successful sign-in.");
            }

            string redirect = !string.IsNullOrWhiteSpace(returnUrl) ? Uri.UnescapeDataString(returnUrl) : "/";
            return Results.Redirect(redirect);
        }).DisableAntiforgery().AllowAnonymous().RequireRateLimiting(CmsRateLimitingRegistration.LoginPolicy);

        app.MapPost("/account/logout", async (HttpContext httpContext, IAuthEventLogger authEventLogger) =>
        {
            // Read who's signing out BEFORE removing the token — the claims come from
            // the Authorization header JwtFromSessionMiddleware just set from this same
            // session, not from re-reading the (about to be cleared) session itself.
            string? userIdClaim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            string userName = httpContext.User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "(bilinmiyor)";

            // Remove JWT from session
            httpContext.Session?.Remove("Jwt");

            if (Guid.TryParse(userIdClaim, out Guid userId))
                await authEventLogger.LogAsync(AuthEventType.Logout, userId, userName, cancellationToken: httpContext.RequestAborted);

            return Results.Redirect("/login");
        }).DisableAntiforgery();

        app.MapGet("/account/logout", () => Results.Redirect("/login"));

        return app;
    }
}
