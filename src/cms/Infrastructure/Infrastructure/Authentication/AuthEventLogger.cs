using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.Logs;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Authentication;

internal sealed class AuthEventLogger(
    ICmsApplicationDbContext db,
    IHttpContextAccessor httpContextAccessor) : IAuthEventLogger
{
    public async Task LogAsync(
        AuthEventType eventType,
        Guid? userId,
        string userNameSnapshot,
        bool saveImmediately = true,
        CancellationToken cancellationToken = default)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;

        db.AuthEvents.Add(new AuthEvent
        {
            EventType = eventType,
            UserId = userId,
            UserNameSnapshot = userNameSnapshot,
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
            CreatedAtUtc = DateTime.UtcNow,
        });

        if (!saveImmediately)
            return;

        // Independent of whatever else the caller is doing in the same request (a
        // login also updates LastLoginDate, for instance) — an audit row should land
        // even if something unrelated later in the request fails, and a login/logout
        // is already over by the time this runs, so there's nothing left to roll back.
        await db.SaveChangesAsync(cancellationToken);
    }
}
