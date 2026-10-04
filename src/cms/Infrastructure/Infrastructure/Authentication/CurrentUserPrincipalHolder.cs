using System.Security.Claims;

namespace Infrastructure.Authentication;

/// <summary>
/// Scoped service that bridges the gap between Blazor Server's SignalR circuit and
/// the infrastructure layer's user-context needs.
///
/// WHY THIS EXISTS
/// ───────────────
/// In Blazor Server interactive mode, components communicate via a persistent SignalR
/// circuit rather than classic HTTP requests.  After the initial page load the
/// ASP.NET Core middleware pipeline (including UseAuthentication) no longer runs for
/// every user action, so <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/>
/// either returns null or returns an HttpContext that has no authentication claims.
///
/// This service acts as a simple in-memory store:
///   - The root layout (<c>AdminLayout</c>) populates it once per circuit via
///     <see cref="Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider"/>.
///   - <see cref="UserContext"/> reads from it as a fallback when the HTTP context
///     is unavailable.
/// </summary>
public sealed class CurrentUserPrincipalHolder
{
    /// <summary>
    /// The authenticated user's <see cref="ClaimsPrincipal"/>.
    /// Set by <c>AdminLayout.OnInitializedAsync</c>; null until populated.
    /// </summary>
    public ClaimsPrincipal? Principal { get; set; }
}
