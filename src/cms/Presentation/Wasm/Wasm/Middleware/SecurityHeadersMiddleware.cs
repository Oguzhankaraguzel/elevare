namespace Wasm.Middleware;

/// <summary>
/// Response headers that close the browser-side holes an admin panel is most
/// exposed to. Set here rather than in a reverse proxy so the protection travels
/// with the app: a self-hosted deployment behind plain Kestrel gets the same
/// treatment as one behind nginx.
/// <para>
/// Deliberately NOT a full Content-Security-Policy. A real <c>script-src</c> would
/// have to allow the CDN this panel loads Bootstrap from, the inline theme script
/// in App.razor, and whatever GrapesJS evaluates inside its canvas — a policy that
/// permissive protects almost nothing while being able to white-screen the editor
/// on any upgrade. The one directive worth having on its own is
/// <c>frame-ancestors</c>, which is what actually stops clickjacking.
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        IHeaderDictionary headers = context.Response.Headers;

        // Stops a browser from second-guessing Content-Type. Without it an uploaded
        // file served as text/plain can still be executed as script if the sniffer
        // decides it looks like one.
        headers["X-Content-Type-Options"] = "nosniff";

        // Nothing legitimately embeds the CMS — the GrapesJS canvas is a same-origin
        // iframe the panel creates itself, not the panel inside someone else's page.
        // Both headers on purpose: frame-ancestors is the modern rule, X-Frame-Options
        // is what older browsers actually obey.
        headers["Content-Security-Policy"] = "frame-ancestors 'none'";
        headers["X-Frame-Options"] = "DENY";

        // Full URLs of admin pages must not leak to third parties. Same-origin
        // navigation keeps the path, cross-origin gets the bare origin, and an
        // HTTPS→HTTP downgrade sends nothing.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Hardware and payment APIs have no place in a content editor. Left off the
        // list: geolocation, which a map block inside the preview may legitimately
        // want to delegate.
        headers["Permissions-Policy"] = "camera=(), microphone=(), payment=(), usb=()";

        await next(context);
    }
}
