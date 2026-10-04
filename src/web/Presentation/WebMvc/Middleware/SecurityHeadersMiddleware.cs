namespace WebMvc.Middleware;

/// <summary>
/// Response headers for the public site. Set in the app rather than a reverse proxy
/// so the protection is part of the deployment, whatever fronts it.
/// <para>
/// The policy is deliberately narrower than the CMS's would suggest, for one
/// concrete reason: Site Codes exists so an operator can inject Google Analytics,
/// Tag Manager, a Meta Pixel or a cookie-consent script into these pages. A
/// <c>script-src</c> restrictive enough to be worth having would break that feature
/// on the first snippet anyone pastes. Locking it down properly means letting the
/// operator declare their own allowed sources — a Site Settings field, not a
/// hardcoded list — and that is a feature, not a header.
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        IHeaderDictionary headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";

        // 'self' rather than 'none': a public page being embedded is a normal thing
        // to want, and only the site's own pages may frame each other by default.
        // This constrains who may frame US; the YouTube and map embeds inside page
        // content are frame-src, which is untouched.
        headers["Content-Security-Policy"] = "frame-ancestors 'self'";
        headers["X-Frame-Options"] = "SAMEORIGIN";

        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // geolocation is intentionally left permitted: map blocks are a first-class
        // content type here and delegating that permission is exactly their job.
        headers["Permissions-Policy"] = "camera=(), microphone=(), payment=(), usb=()";

        await next(context);
    }
}
