using Application.Features.Commands.Analytics.RecordPageClick;
using Application.Features.Commands.Analytics.RecordPageDuration;
using Application.Features.Commands.Analytics.RecordPageView;
using MediatR;
using SharedKernel.Concrete;
using WebMvc.RateLimiting;

namespace WebMvc.Endpoints;

/// <summary>
/// Fire-and-forget telemetry endpoints called by the small tracking script in
/// _Layout.cshtml. Visitors are identified only by a random GUID cookie — no
/// personal data is collected.
/// <para>
/// That cookie is a statistics cookie, not one the site needs to work, so it is
/// only set once the visitor has allowed analytics in the cookie banner (the
/// request says so). Without that, each hit gets a throwaway id: views are still
/// counted, they just are not tied to one browser. A cookie left over from an
/// earlier "yes" is removed as soon as a request arrives without consent.
/// </para>
/// </summary>
public static class TrackingEndpoints
{
    private const string VisitorCookieName = "elv_vid";

    private static readonly string[] BotUserAgentFragments =
    [
        "bot", "crawler", "spider", "slurp", "bingpreview",
        "facebookexternalhit", "headless", "lighthouse", "pingdom"
    ];

    public static WebApplication MapTrackingEndpoints(this WebApplication app)
    {
        app.MapPost("/api/track/view", async (TrackViewRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (IsBot(http) || string.IsNullOrWhiteSpace(request.Path))
                return Results.Ok(new TrackViewResponse(0));

            Guid visitorId = GetOrCreateVisitorId(http, request.Consent);

            Result<long> result = await sender.Send(
                new RecordPageViewCommand(request.Path, request.Title, visitorId), ct);

            return Results.Ok(new TrackViewResponse(result.IsSuccess ? result.Value : 0));
        }).RequireRateLimiting(RateLimitPolicies.Telemetry);

        app.MapPost("/api/track/duration", async (TrackDurationRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!IsBot(http))
                await sender.Send(new RecordPageDurationCommand(request.Id, request.Seconds), ct);

            return Results.NoContent();
        }).RequireRateLimiting(RateLimitPolicies.Telemetry);

        app.MapPost("/api/track/click", async (TrackClickRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!IsBot(http) && !string.IsNullOrWhiteSpace(request.Path) && !string.IsNullOrWhiteSpace(request.ElementLabel))
            {
                Guid visitorId = GetOrCreateVisitorId(http, request.Consent);
                await sender.Send(new RecordPageClickCommand(request.Path, request.ElementLabel, visitorId), ct);
            }

            return Results.NoContent();
        }).RequireRateLimiting(RateLimitPolicies.Telemetry);

        return app;
    }

    internal static bool IsBot(HttpContext http)
    {
        string userAgent = http.Request.Headers.UserAgent.ToString();
        if (userAgent.Length == 0)
            return true; // real browsers always send a user agent

        return BotUserAgentFragments.Any(fragment =>
            userAgent.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static Guid GetOrCreateVisitorId(HttpContext http, bool consent)
    {
        if (!consent)
        {
            if (http.Request.Cookies.ContainsKey(VisitorCookieName))
                http.Response.Cookies.Delete(VisitorCookieName, new CookieOptions { Secure = true, SameSite = SameSiteMode.Lax });
            return Guid.NewGuid();
        }

        if (http.Request.Cookies.TryGetValue(VisitorCookieName, out string? raw)
            && Guid.TryParse(raw, out Guid existing))
            return existing;

        var visitorId = Guid.NewGuid();
        http.Response.Cookies.Append(VisitorCookieName, visitorId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = false
        });
        return visitorId;
    }
}
