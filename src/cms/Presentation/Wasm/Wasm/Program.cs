using System.Globalization;
using Application.Abstraction.Services;
using Application.DependencyInjection;
using Hangfire;
using Hangfire.PostgreSql;
using Infrastructure.Authentication;
using Infrastructure.Backup;
using Infrastructure.DependencyInjection;
using Infrastructure.Reminders;
using Infrastructure.Retention;
using Infrastructure.Sitemap;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Persistence.DependencyInjection;
using Persistence.Seed;
using Serilog;
using Wasm;
using Wasm.Components;
using Wasm.Components.Services;
using Wasm.Configuration;
using Wasm.Endpoints;
using Wasm.Middleware;
using Wasm.RateLimiting;

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
    // Application = 'Elevare.Cms'.
    loggerConfig.Enrich.WithProperty("Application", "Elevare.Cms");

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

// ── Data protection keys ─────────────────────────────────────────────────────
// The admin's JWT lives in the session cookie, which is encrypted with these keys.
// By default they are written inside the container and vanish with it, so every
// `docker compose up` after a `down` signs everybody out and invalidates any
// antiforgery token already on a page. Setting DataProtection:KeyPath points them
// at a mounted volume instead. Left unset (the plain `dotnet run` case) the
// framework default applies and nothing changes.
string? keyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(keyPath))
{
    Directory.CreateDirectory(keyPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
        // Pinned so the keys stay readable if the app is ever renamed or moved.
        .SetApplicationName("Elevare.Cms");
}

// ── Integration secrets (SMTP, S3/CDN) from the database ───────────────────────
// Read straight from Postgres before the DI container exists (see
// IntegrationSecretsBootstrap for why), then layered on top of appsettings.json —
// added last, so a value entered on the CMS's "Sırlar" screen wins over the file,
// and an empty database changes nothing. Unlike a plain AddInMemoryCollection,
// this source can be reloaded later (see IntegrationSecretsConfigurationProvider)
// — registered as IIntegrationSecretsReloader below — so a save from the "Sırlar"
// screen reaches every IOptionsMonitor<EmailOptions>/<CaptchaOptions>/
// <ObjectStorageOptions> consumer without a restart.
await LoadIntegrationSecretOverridesAsync(builder, keyPath);

// Lets the Secrets and Site Settings screens say where a value comes from — the
// database or the server's own configuration (.env) — instead of showing a field
// empty while the setting is in fact in effect.
builder.Services.AddSingleton<IConfigurationInspector, ConfigurationInspector>();

static async Task LoadIntegrationSecretOverridesAsync(WebApplicationBuilder builder, string? keyPath)
{
    string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        return;

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

// ── Session store ─────────────────────────────────────────────────────────────
// The admin's JWT lives in the session, so where the session is kept decides
// whether restarting the CMS signs everybody out. In-process memory is the default
// and is fine for a single instance you rarely restart; pointing
// Redis:ConnectionString at a server makes sessions survive a restart and lets more
// than one CMS instance share them.
// Conditional rather than required, because Redis is optional in this stack —
// the same shape as DataProtection:KeyPath above.
string? redisConnection = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        // Prefixed so the CMS's session keys cannot collide with the public site's
        // cache entries when both point at the same Redis.
        options.InstanceName = "elevare-cms:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ── Service registrations ─────────────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// ── Hangfire ──────────────────────────────────────────────────────────────────
string hangfireConn = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(pg => pg.UseNpgsqlConnection(hangfireConn), new PostgreSqlStorageOptions
    {
        // Unlike Hangfire.SqlServer, this provider has no LISTEN/NOTIFY-driven
        // "zero means reactive" mode — TimeSpan.Zero is rejected outright. Half a
        // second keeps job pickup close to instant without hammering the database.
        QueuePollInterval = TimeSpan.FromMilliseconds(500),
        InvisibilityTimeout = TimeSpan.FromMinutes(5),
        DistributedLockTimeout = TimeSpan.FromMinutes(5)
    }));
builder.Services.AddHangfireServer();

// ── Blazor ────────────────────────────────────────────────────────────────────
// The page builder is the reason the circuit's message ceiling is raised.
//
// Saving a page returns the whole canvas from JS to .NET in one interop result:
// exported HTML, exported CSS, and the GrapesJS project JSON. That result travels
// browser-to-server over the circuit, so it is bounded by MaximumReceiveMessageSize
// — whose default is 32 KB. A page with a hero, a slider, a gallery, a video, a
// form and a footer measured 58 KB, so the hub closed the connection mid-save and
// the save never completed. The symptom was baffling from the editor's side: the
// Save button span forever, and the browser console showed only a dropped circuit.
//
// The ceiling is per message, not per circuit, so this costs nothing until a page
// actually gets large. 4 MB is roughly two orders of magnitude above the page that
// broke, which leaves room for genuinely big pages while still bounding what one
// authenticated editor can make the server buffer at once.
IRazorComponentsBuilder razorComponents = builder.Services.AddRazorComponents();
razorComponents.AddInteractiveServerComponents()
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 4 * 1024 * 1024);
razorComponents.AddInteractiveWebAssemblyComponents();

string apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5001";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });

// ── Shared UI services ────────────────────────────────────────────────────────
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<LoadingService>();
builder.Services.AddScoped<CmsLocalizer>();
builder.Services.AddScoped<SiteIdentityState>();

// ── Localization ──────────────────────────────────────────────────────────────
builder.Services.AddLocalization();

// ── Rate limiting ─────────────────────────────────────────────────────────────
// Anonymous surface only; see CmsRateLimitOptions.
builder.Services.AddCmsRateLimiting(builder.Configuration);

// ── HSTS ────────────────────────────────────────────────────────────────────────
// A year rather than the framework's 30-day default: this admin panel is HTTPS-only
// in every deployment, so telling browsers to remember that for longer is pure upside.
// IncludeSubDomains is left off on purpose — it would speak for sibling subdomains
// this app knows nothing about.
builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));

WebApplication app = builder.Build();

// ── HTTP pipeline ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Must run before anything below inspects Request.Scheme/RemoteIpAddress — behind
// nginx (or any reverse proxy) every request otherwise looks like plain HTTP from
// 127.0.0.1, which breaks HTTPS detection, secure cookies and IP-based logging.
// Default KnownProxies/KnownNetworks already trust loopback, which is exactly
// where nginx forwards from here, so no extra configuration is needed.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Security headers before anything else can short-circuit the pipeline, so every
// response carries them — including the ones static files and endpoints produce.
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseHttpsRedirection();
app.UseStaticFiles();

// Uploaded media is NOT served from here — see MediaEndpoints for why the static
// file middleware cannot reach it.

// ── Auto-detect culture on first visit ────────────────────────────────────────
// If the browser has never visited before (no culture cookie), read the
// Accept-Language header and persist the detected culture as a cookie so that
// the LanguageSwitcher shows the correct active state from the very first load.
app.Use(async (context, next) =>
{
    string cookieName = CookieRequestCultureProvider.DefaultCookieName;
    if (!context.Request.Cookies.ContainsKey(cookieName))
    {
        string acceptLang = context.Request.Headers.AcceptLanguage.FirstOrDefault() ?? "";
        string culture = acceptLang.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? "tr" : "en";
        context.Response.Cookies.Append(
            cookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture, culture)),
            new CookieOptions
            {
                Expires    = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite   = SameSiteMode.Lax
            });
    }
    await next();
});

// ── Request localization (culture cookie) ─────────────────────────────────────
string[] supportedCultures = ["tr", "en"];
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("tr")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

// Session + JWT middleware must run before endpoint execution
app.UseSession();
app.UseMiddleware<JwtFromSessionMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Fingerprinted wwwroot/CSS/JS assets are served via endpoint routing, so they must
// be marked AllowAnonymous to bypass the FallbackPolicy.
app.MapStaticAssets().AllowAnonymous();

// _framework/blazor.web.js is served by this endpoint, not MapStaticAssets above —
// without AllowAnonymous here the FallbackPolicy 401s it before Blazor's own script
// tag can even load, which breaks every page's boot, login included: the outer host
// page (App.razor) is one unauthenticated ASP.NET endpoint for every route, while
// per-page access control happens one layer in, via AuthorizeRouteView in
// Routes.razor (see Login.razor's own [AllowAnonymous] for the pattern this mirrors).
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Wasm.Client._Imports).Assembly)
    .AllowAnonymous();

// ── Database migrate + seed ───────────────────────────────────────────────────
// Must run before anything below touches the database. Hangfire in particular
// installs its own tables on first use, and it cannot create the database itself
// — against a brand-new server (a fresh `docker compose up`, or a first clone
// pointed at an empty SQL Server) it fails with "Cannot open database". EF's
// MigrateAsync is what creates the database, so it has to come first.
await DatabaseSeeder.SeedAsync(app.Services);

// ── Hangfire Dashboard ────────────────────────────────────────────────────────
app.UseHangfireDashboard("/admin/jobs/dashboard", new DashboardOptions
{
    DashboardTitle = "Elevare CMS – Job Dashboard",
    Authorization = [new HangfireAuthFilter()]
});

// ── Register recurring jobs ───────────────────────────────────────────────────
RecurringJob.AddOrUpdate<SitemapJob>(
    recurringJobId: "sitemap-generation",
    methodCall: job => job.ExecuteAsync(CancellationToken.None),
    cronExpression: "0 */2 * * *");   // every 2 hours

// …and once now. The cron above only fires on the next even hour, so a freshly
// installed site answered /sitemap.xml with 404 for up to two hours — long enough
// for the operator to file it as a bug. Enqueued rather than run inline so startup
// is not held up by it, and it is idempotent, so the extra run after every restart
// costs one rebuild of a cache that was about to be rebuilt anyway.
BackgroundJob.Enqueue<SitemapJob>(job => job.ExecuteAsync(CancellationToken.None));

// Images uploaded before derived sizes existed get them once, in the background;
// on a library that is already up to date this is a single query. See the job.
BackgroundJob.Enqueue<Infrastructure.Files.ImageDerivativesBackfillJob>(job => job.ExecuteAsync(CancellationToken.None));

RecurringJob.AddOrUpdate<ReminderJob>(
    recurringJobId: "reminder-check",
    methodCall: job => job.ExecuteAsync(CancellationToken.None),
    cronExpression: "* * * * *");      // every minute

// Weekly full database backup, followed by pruning older ones (see BackupOptions).
// Sundays at 02:00 UTC — before the retention trim, so the backup captures the data
// that trim is about to delete.
RecurringJob.AddOrUpdate<DatabaseBackupJob>(
    recurringJobId: "database-backup",
    methodCall: job => job.ExecuteAsync(CancellationToken.None),
    cronExpression: "0 2 * * 0");

// Trims page views, click tracking and application logs to their retention window
// (see RetentionOptions). Runs at 03:20 UTC — off-peak, and deliberately not on the
// hour so it does not contend with the sitemap job.
RecurringJob.AddOrUpdate<DataRetentionJob>(
    recurringJobId: "data-retention",
    methodCall: job => job.ExecuteAsync(CancellationToken.None),
    cronExpression: "20 3 * * *");

// ── Auth endpoints (cookie sign-in requires real HTTP context, not SignalR) ───
// Map auth endpoints from dedicated endpoint registration
app.MapAuthEndpoints();

// ── Uploaded media (/uploads/...) ─────────────────────────────────────────────
app.MapMediaEndpoints();
app.MapFormAttachmentEndpoints();

// ── Site Codes CSS, replayed into the page builder's own canvas ──────────────
app.MapCanvasPreviewEndpoints();

// ── Culture switching endpoint ────────────────────────────────────────────────
app.MapGet("/culture/set", (string culture, string? redirectUri, HttpContext ctx) =>
{
    string[] allowed = ["tr", "en"];
    if (!allowed.Contains(culture)) culture = "tr";

    ctx.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture, culture)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        });

    // redirectUri comes straight off the query string, so it has to be treated as
    // attacker-controlled even though the language switcher only ever passes the
    // current path. Only a same-site absolute path is allowed through: anything
    // starting "//" or "/\" is a protocol-relative URL that would bounce the visitor
    // to another origin (an open redirect), and an absolute "http(s)://…" likewise.
    // Legitimate callers always pass a local path like "/templates", so this rejects
    // only the abuse.
    bool isLocal = !string.IsNullOrWhiteSpace(redirectUri)
        && redirectUri.StartsWith('/')
        && !redirectUri.StartsWith("//", StringComparison.Ordinal)
        && !redirectUri.StartsWith("/\\", StringComparison.Ordinal);

    return Results.Redirect(isLocal ? redirectUri! : "/admin");
}).AllowAnonymous();

// Anonymous on purpose: the app sets a fallback authorization policy, which
// otherwise makes /health answer 401 — useless to a container healthcheck, an
// uptime monitor or a load balancer, all of which read that as "down".
app.MapHealthChecks("/health").AllowAnonymous();

await app.RunAsync();
