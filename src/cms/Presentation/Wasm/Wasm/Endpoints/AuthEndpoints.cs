using System.IdentityModel.Tokens.Jwt;
using SharedKernel.Abstraction.Messaging;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using SharedKernel.Concrete;
using Application.Abstraction.Services;
using Application.Features.Auth.Login;
using Application.Features.Commands.Users.RequestPasswordReset;
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

            // An administrator typed this password: no session until the user picks
            // their own. The one-time link goes in the URL like a mailed one would.
            if (r.Value.PasswordChangeToken is { } changeToken)
                return Results.Redirect($"/account/set-password?token={Uri.EscapeDataString(changeToken)}&mode=change");

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

        // A real form post, not a Blazor call, for the same reason as sign-in: the
        // rate limiter only sees HTTP requests. The answer is the same whatever
        // happened, so the page cannot be used to find out which addresses exist.
        app.MapPost("/account/forgot-password/send", async (
            HttpContext httpContext,
            ISender sender,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            [FromForm] string identifier) =>
        {
            if (CmsPublicUrl(configuration, httpContext.Request) is { } cmsUrl)
            {
                await sender.Send(new RequestPasswordResetCommand(identifier ?? "", cmsUrl));
            }
            else
            {
                loggerFactory.CreateLogger(typeof(AuthEndpoints)).LogWarning(
                    "A password reset was requested, but the CMS's own address is not configured, so no link "
                    + "could be built. Set Cms:PublicUrl (CMS_PUBLIC_URL).");
            }

            return Results.Redirect("/account/forgot-password?sent=1");
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

    /// <summary>
    /// The address a reset link points at. Taken from configuration, never from the
    /// request: the Host header is the sender's to choose, and a link built from it
    /// would let anyone mail a real user a "reset" link to a server of their own.
    /// Cms:PublicUrl, else the JWT issuer, which a deployment usually sets to the
    /// CMS's address. A localhost address is only good for a request that itself
    /// came to localhost — the issuer's default would otherwise mail production users
    /// a link to their own machine.
    /// </summary>
    private static string? CmsPublicUrl(IConfiguration configuration, HttpRequest request)
    {
        bool localRequest = request.Host.Host is "localhost" or "127.0.0.1" or "[::1]";

        foreach (string? candidate in new[] { configuration["Cms:PublicUrl"], configuration["Jwt:Issuer"] })
        {
            if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                && (!uri.IsLoopback || localRequest))
                return uri.GetLeftPart(UriPartial.Authority);
        }

        return null;
    }
}
