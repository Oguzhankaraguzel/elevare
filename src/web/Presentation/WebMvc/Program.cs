using System.Globalization;
using System.IO.Compression;
using Application.Abstraction.Services;
using Application.DependencyInjection;
using Application.Services;
using Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Rewrite;
using Persistence.DependencyInjection;
using Serilog;
using SharedKernel.Concrete;
using WebMarkupMin.AspNetCoreLatest;
using WebMarkupMin.Core;
using WebMvc;
using WebMvc.Configuration;
using WebMvc.Controllers;
using WebMvc.Endpoints;
using WebMvc.Middleware;
using WebMvc.Middleware.Rules;
using WebMvc.RateLimiting;
using WebMvc.Routing;
using WebOptimizer;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// No "Server: Kestrel" on responses: naming the server software tells a visitor
// nothing useful and a scanner exactly what to try.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// ── Serilog ──────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig.ReadFrom.Configuration(context.Configuration);

    // Both applications can ship to the same Seq, and nothing in an event said
    // which one wrote it — SourceContext only hints, and MachineName is a container
    // id that changes on every recreate. This is the field to filter on:
    // Application = 'Elevare.Web'.
    loggerConfig.Enrich.WithProperty("Application", "Elevare.Web");

    // Seq is added here rather than in appsettings' WriteTo, so that it is added
    // only when somebody has actually said where Seq is. It used to be declared
    // there with a hardcoded http://localhost:5341 — which inside a container is
    // the container itself, where nothing listens. The sink buffers and drops on
    // its own, so nothing crashed and nothing arrived: structured logging looked
    // configured and went nowhere. Empty (the default) leaves Console as the only
    // sink, which Docker captures and `docker compose logs` reads.
    string? seqUrl = context.Configuration["Serilog:SeqUrl"];
    if (!string.IsNullOrWhiteSpace(seqUrl))
    {
        string? seqApiKey = context.Configuration["Serilog:SeqApiKey"];
        // Invariant on purpose: a log line's numbers and dates must not change shape
        // because the server happens to run under a Turkish locale.
        loggerConfig.WriteTo.Seq(
            seqUrl,
            apiKey: string.IsNullOrWhiteSpace(seqApiKey) ? null : seqApiKey,
            formatProvider: CultureInfo.InvariantCulture);
    }
});

// ── CAPTCHA secret from the database ────────────────────────────────────────────
// Read straight from Postgres before AddWebInfrastructure binds CaptchaOptions —
// see IntegrationSecretsBootstrap. The value is encrypted with the CMS's Data
// Protection key ring, so this needs the same DataProtection:KeyPath the CMS
// mounts (see docker-compose.yml) to decrypt it; unset here, the CAPTCHA secret
// simply falls back to Captcha:SecretKey from appsettings.json/env, same as before.
// The source this loads into is reloadable (see IntegrationSecretsConfigurationProvider,
// registered as IIntegrationSecretsReloader below) — the CMS calls
// POST /api/secrets/reload (SecretsEndpoints) after saving a Captcha:* value, so a
// new secret key applies here without restarting this app either.
await LoadIntegrationSecretOverridesAsync(builder);

static async Task LoadIntegrationSecretOverridesAsync(WebApplicationBuilder builder)
{
    string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        return;

    string? keyPath = builder.Configuration["DataProtection:KeyPath"];
    // Not `using`: the provider built from this factory outlives this local
    // function, holding the factory for the rest of the app's lifetime so
    // ReloadAsync can keep logging after startup.
#pragma warning disable CA2000
    Microsoft.Extensions.Logging.ILoggerFactory bootstrapLoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddSerilog());
#pragma warning restore CA2000
    Microsoft.Extensions.Logging.ILogger bootstrapLogger = bootstrapLoggerFactory.CreateLogger("IntegrationSecretsBootstrap");

    Dictionary<string, string?> overrides = await IntegrationSecretsBootstrap.LoadAsync(connectionString, keyPath, bootstrapLogger);

    var provider = new IntegrationSecretsConfigurationProvider(overrides, connectionString, keyPath, bootstrapLoggerFactory);
    // ConfigurationManager implements IConfigurationBuilder.Add explicitly, so it is
    // invisible through the concrete type WebApplicationBuilder.Configuration exposes —
    // Sources.Add is the same operation via a member that isn't hidden that way.
    builder.Configuration.Sources.Add(new IntegrationSecretsConfigurationSource(provider));
    builder.Services.AddSingleton<IIntegrationSecretsReloader>(provider);
}

// ── Application / Persistence (reads the CMS's database; never writes to it) ──
builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddWebInfrastructure(builder.Configuration);

// ── MVC ──────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// Razor's HTML encoder escapes everything outside Basic Latin by default, so a
// title read "O&#x11F;uzhan KARAG&#xDC;ZEL" in the page source. Allowing every
// Unicode range keeps the letters as they are; what makes output safe — escaping
// <, >, &, quotes — is not affected by this, so Html.Raw is not needed anywhere.
builder.Services.AddWebEncoders(options =>
    options.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));
builder.Services.AddHealthChecks();

// ── Routing ──────────────────────────────────────────────────────────────────
builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
    options.ConstraintMap.Add("existingLanguage", typeof(ExistingLanguageRouteConstraint));
});
builder.Services.AddScoped<SlugRouteValueTransformer>();

builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status301MovedPermanently;
});

// ── Response Compression (Brotli + Gzip) ─────────────────────────────────────
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
    [
        "text/html",
        "text/css",
        "application/javascript",
        "application/json",
        "image/svg+xml",
        "application/xml",
        "text/xml"
    ]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Optimal);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Optimal);

// ── HTML Minification ────────────────────────────────────────────────────────
builder.Services
    .AddWebMarkupMin(options =>
    {
        options.AllowMinificationInDevelopmentEnvironment = true;
        options.AllowCompressionInDevelopmentEnvironment = true;
        options.DisablePoweredByHttpHeaders = true;
    })
    .AddHtmlMinification(options =>
    {
        options.MinificationSettings.RemoveRedundantAttributes = true;
        options.MinificationSettings.RemoveOptionalEndTags = false;
        options.MinificationSettings.WhitespaceMinificationMode = WhitespaceMinificationMode.Aggressive;
    });

// ── Output Caching ─────────────────────────────────────────────────────────
builder.Services.AddOutputCache(options =>
{
    // Rendered pages: an hour, evicted by tag on every CMS change (see PageController).
    // The query string is not part of the key as a whole — the default would cache
    // "?utm_source=…" and "?fbclid=…" as separate pages, one entry per shared link —
    // only the listing parameters are (ListingQuery), because only they change what
    // a page shows.
    options.AddPolicy(PageController.OutputCachePolicy, policy => policy
        .Expire(TimeSpan.FromHours(1))
        .Tag(PageController.OutputCacheTag)
        .SetVaryByQuery(Array.Empty<string>())
        .VaryByValue(context => new KeyValuePair<string, string>(
            "listing",
            ListingQuery.CacheKey(context.Request.Query.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value.ToString()))))));
});

// ── Rate Limiting ────────────────────────────────────────────────────────────
// Applies to the /api/* endpoints only; see RateLimitPolicies for the reasoning.
builder.Services.AddElevareRateLimiting(builder.Configuration);

// ── JS/CSS Minification ──────────────────────────────────────────────────────
// HTML is minified above via WebMarkupMin; this covers the static .js/.css files
// themselves (wwwroot/js/*.js, wwwroot/css/*.css). Requests are intercepted and
// minified on the fly (cached after the first request) — no build step needed.
builder.Services.AddWebOptimizer(
    pipeline =>
    {
        pipeline.MinifyJsFiles("js/**/*.js");
        pipeline.MinifyCssFiles("css/**/*.css");
    },
    options =>
    {
        // Disk cache off, memory cache on.
        //
        // WebOptimizer's disk cache writes under "obj", which only exists as a
        // writable folder on a developer's machine. In the container the app runs as
        // a non-root user against a read-only /app, and in IIS the app pool identity
        // usually cannot write into the publish folder either — so the very first
        // request for every "?v=..." asset threw UnauthorizedAccessException and
        // answered 500. The page still rendered, which is what made it easy to miss:
        // it simply arrived with no CSS and no JavaScript, so every managed form fell
        // back to a plain GET and every interactive block stopped working.
        //
        // Minifying is cheap and the memory cache absorbs it after the first hit,
        // so nothing is lost by not persisting the result across restarts.
        options.EnableDiskCache = false;
        options.EnableMemoryCache = true;
    });

// ── HSTS ────────────────────────────────────────────────────────────────────────
// A year rather than the framework's 30-day default; the public site is HTTPS-only.
// IncludeSubDomains stays off so this never speaks for the CMS or other subdomains.
builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));

WebApplication app = builder.Build();

// ── Warm the CMS-owned state snapshots so the very first request has real data ──
// (SiteStateRefreshHostedService keeps them refreshed periodically after this;
// a DB outage at startup therefore only delays the snapshots, it doesn't stop the app.)
using (IServiceScope startupScope = app.Services.CreateScope())
{
    try
    {
        ILanguageDirectory languageDirectory =
            startupScope.ServiceProvider.GetRequiredService<ILanguageDirectory>();
        Result languages = await languageDirectory.RefreshAsync();
        if (languages.IsFailure)
            Log.Warning("{Error} The background refresher will retry.", languages.Error.Description);

        IMaintenanceState maintenanceState =
            startupScope.ServiceProvider.GetRequiredService<Application.Abstraction.Services.IMaintenanceState>();
        Result maintenance = await maintenanceState.RefreshAsync();
        if (maintenance.IsFailure)
            Log.Warning("{Error} The background refresher will retry.", maintenance.Error.Description);

        Result wwwRedirect = await startupScope.ServiceProvider.GetRequiredService<IWwwRedirectState>().RefreshAsync();
        if (wwwRedirect.IsFailure)
            Log.Warning("{Error} The background refresher will retry.", wwwRedirect.Error.Description);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Initial site-state warm-up threw unexpectedly; the background refresher will retry.");
    }
}

// ── HTTP Pipeline ────────────────────────────────────────────────────────────
// 0. Must run before anything below inspects Request.Scheme/RemoteIpAddress —
// behind nginx (or any reverse proxy) every request otherwise looks like plain
// HTTP from 127.0.0.1, which breaks HTTPS detection, secure cookies and IP-based
// rate limiting/logging. Default KnownProxies/KnownNetworks already trust
// loopback, which is exactly where nginx forwards from here.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// 1. Global exception handling (must be first to catch everything)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// 1.5 Security headers, immediately after it so error pages carry them too — a
// 500 rendered without X-Content-Type-Options is still a response a browser sniffs.
app.UseMiddleware<SecurityHeadersMiddleware>();

// 2. HSTS - Rewriter - HttpsRedirection (production only)
//
// Skipped for /api: these rules exist to give VISITORS one canonical address, and
// a machine calling this app is not a visitor. The CMS's cache-clear POST comes
// straight across the container network as http://web:8080 — SeoRedirectRule would
// answer it with a 301 to https://www.web:8080, and HttpClient, following the
// redirect, downgrades a POST to a GET (RFC 9110 for 301). The GET matches no
// route, falls through to the page catch-all, and the CMS is told the website
// "returned an error" while the site's own log records a request for a page called
// "api/cache/clear". Every part of that is correct behaviour for a browser and
// wrong for this call, so the call is simply not put through it.
if (!app.Environment.IsDevelopment())
{
    app.UseWhen(
        static context => !context.Request.Path.StartsWithSegments("/api"),
        branch =>
        {
            branch.UseHsts();
            branch.UseRewriter(
                new RewriteOptions().Add(
                    new SeoRedirectRule()
                    )
                );
            branch.UseHttpsRedirection();
        });
}

// 4. Response compression (before static files so responses are compressed)
app.UseResponseCompression();

// 4.4 Cache headers for what WebOptimizer serves. It answers /js and /css itself,
// ahead of the static-file middleware whose OnPrepareResponse sets the year-long
// cache below — so the minified scripts went out with no Cache-Control at all
// (measured on the live site: ETag, Last-Modified, nothing else), and Lighthouse
// listed every one of them under "efficient cache lifetimes". The URLs carry a
// content hash (asp-append-version), which is what makes "immutable" true.
app.Use(async (context, next) =>
{
    PathString path = context.Request.Path;
    if ((path.StartsWithSegments("/js") || path.StartsWithSegments("/css")) && context.Request.Query.ContainsKey("v"))
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("Cache-Control"))
                context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
            return Task.CompletedTask;
        });
    }
    await next();
});

// 4.5 JS/CSS minification — must run before static file serving so it can
// intercept and transform matching requests.
app.UseWebOptimizer();

// 5. Static files with caching headers
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    }
});

// 5.5 Maintenance mode gate — after static files (so a custom maintenance page's
//     assets still load), before everything that renders actual content.
app.UseMiddleware<MaintenanceModeMiddleware>();

// 6. Routing
app.UseRouting();

// 7. Authorization
app.UseAuthorization();

// 8. Output caching — after routing, because [OutputCache] is endpoint metadata:
//    ahead of UseRouting there is no endpoint yet, the middleware sees no policy,
//    and nothing was ever cached (every page view re-ran the queries, the template
//    resolvers, the view and the minifier).
app.UseOutputCache();

// 9. HTML minification — behind the output cache, so what gets cached is the
//    minified page and a cache hit skips the minifier too.
app.UseWebMarkupMin();

// 10. Rate limiting — after UseRouting so the endpoint's own policy is known, and
//     after output caching so a cached page never spends a request budget.
app.UseRateLimiter();

// ── Route Mapping ────────────────────────────────────────────────────────────
app.MapStaticAssets();

// Anonymous page-view telemetry (see the tracking script in _Layout.cshtml).
app.MapTrackingEndpoints();

// Client-side JS error reporting (see elevare-interactions.js).
app.MapLoggingEndpoints();

// GrapesJS-authored form submissions (see elevare-interactions.js).
app.MapFormEndpoints();

// Live search for the "Arama Kutusu" block (see elevare-interactions.js).
app.MapSearchEndpoints();

// Cache invalidation, called by the CMS's "Temizle" action (see CacheEndpoints).
app.MapCacheEndpoints();

// Integration-secret reload, called by the CMS right after a "Sırlar" save
// touches a Captcha:* key (see SecretsEndpoints).
app.MapSecretsEndpoints();

// Multi-language slug route: /{languageCode}/{slug}. The "existingLanguage"
// constraint requires a real, known language code as the literal first segment,
// so this can never accidentally match "/" or "/sitemap.xml".
app.MapDynamicControllerRoute<SlugRouteValueTransformer>(
    "{languageCode:existingLanguage}/{**slug}");

// SitemapController is attribute-routed ([HttpGet("sitemap.xml")] etc.) and
// PageController is only ever reached via the routes above/below — there is no
// conventional "{controller}/{action}/{id?}" route because every page, including
// the homepage (reserved slug "home"), goes through PageController exclusively.

// True last-resort fallback: only reached when NOTHING else matched (no language
// prefix, no attribute-routed controller) — by construction that means "default
// language page slug", including the bare root ("/") which PageController
// resolves to the reserved "home" slug. MapFallback* endpoints are always
// evaluated after every other endpoint (an earlier attempt using a second
// MapDynamicControllerRoute with an explicit `order` value did NOT prevent it
// from capturing "/").
// Uniform probe with the CMS, for container healthchecks and uptime monitors.
app.MapHealthChecks("/health");

app.MapFallbackToController("Index", "Page");

await app.RunAsync();
