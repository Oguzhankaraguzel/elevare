using Application.Abstraction.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Rewrite;
using System;
using System.IO;
using System.Linq;

namespace WebMvc.Middleware.Rules;

public class SeoRedirectRule : IRule
{
    public void ApplyRule(RewriteContext context)
    {
        HttpRequest req = context.HttpContext.Request;
        HostString host = req.Host;

        // Ignore redirections in the development environment (localhost)
        if (host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Host.Equals("127.0.0.1", StringComparison.Ordinal) ||
            host.Host.Equals("::1", StringComparison.Ordinal))
        {
            return;
        }

        // Static assets (css/js/images/uploaded files/robots.txt/...) never need
        // SEO canonicalization — a page slug is worth having in exactly one casing
        // for search engines, but a file's real name on disk is whatever it is, and
        // rewriting the request for it here only risks turning a normal asset
        // request into a redirect (this rule ran before UseStaticFiles). This also
        // runs before UseStaticFiles either way, so HTTP→HTTPS is still handled —
        // just by the generic UseHttpsRedirection() right after this rewriter,
        // rather than by rule 1 below.
        if (!string.IsNullOrEmpty(Path.GetExtension(req.Path.Value)))
            return;

        bool needsRedirect = false;
        string newHost = host.Host;
        string newPath = req.Path.Value ?? "/";
        string scheme = req.Scheme;

        // Rule 1: Redirect HTTP to HTTPS
        if (scheme != "https")
        {
            scheme = "https";
            needsRedirect = true;
        }

        // Rule 2: Add 'www.' prefix if it is missing — unless the CMS's
        // "Advanced.RedirectToWww" setting has been switched off, in which case both
        // addresses are served as they were asked for.
        bool redirectToWww = context.HttpContext.RequestServices.GetRequiredService<IWwwRedirectState>().IsEnabled;
        if (redirectToWww && !newHost.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            newHost = "www." + newHost;
            needsRedirect = true;
        }

        // Preserve the port if it exists (useful for non-standard testing environments)
        if (host.Port.HasValue && host.Port != 80 && host.Port != 443)
        {
            newHost = $"{newHost}:{host.Port}";
        }

        // Rule 3: Convert the path to lowercase if it contains uppercase letters
        if (newPath.Any(char.IsUpper))
        {
#pragma warning disable CA1308 
            newPath = newPath.ToLowerInvariant();
#pragma warning restore CA1308 
            needsRedirect = true;
        }

        // If any redirection rule was triggered, issue a 301 Permanent Redirect and terminate the pipeline
        if (needsRedirect)
        {
            // req.Path.Value is already decoded (that's how ASP.NET Core hands it
            // out), and newPath above is built from it — so a request for a path
            // with non-ASCII characters (Turkish or otherwise) would put those
            // characters straight into a Location header. HTTP headers only allow
            // ASCII, so .NET throws before the redirect can even be sent. Routing
            // the rebuilt path back through PathString re-encodes it the same way
            // the request itself was encoded; the query string is left untouched
            // because QueryString.Value is already in its encoded form.
            string encodedPath = new PathString(newPath).ToUriComponent();
            string newUrl = $"{scheme}://{newHost}{req.PathBase}{encodedPath}{req.QueryString}";
            context.HttpContext.Response.StatusCode = StatusCodes.Status301MovedPermanently;
            context.HttpContext.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Location] = newUrl;
            context.Result = RuleResult.EndResponse;
        }
    }
}
