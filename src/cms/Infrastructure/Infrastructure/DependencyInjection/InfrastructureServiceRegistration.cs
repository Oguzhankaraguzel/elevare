using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Email;
using Application.Abstraction.Services.Files;
using Application.Abstraction.Services.Storage;
using Application.Abstraction.Services.Backup;
using Domain.Entities.Logs;
using Infrastructure.Authentication;
using Infrastructure.Backup;
using Infrastructure.Caching;
using Infrastructure.Configuration;
using Infrastructure.Email;
using Infrastructure.Files;
using Infrastructure.Health;
using Infrastructure.Retention;
using Infrastructure.Security;
using Infrastructure.Sitemap;
using Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SpectraUtils.Extensions;

namespace Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Fail here, loudly, rather than at the first login with a cryptic crypto
        // error. The shipped appsettings.json leaves this blank on purpose: a signing
        // key committed to a public repository lets anyone forge an admin token.
        string jwtSecret = configuration["Jwt:SecretKey"] ?? "";
        if (jwtSecret.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SecretKey is missing or shorter than 32 characters. Set it in appsettings, "
                + "user-secrets, or the Jwt__SecretKey environment variable. Generate one with: "
                + "openssl rand -base64 48");
        }

        // ── JWT Bearer — added as secondary scheme; AdminCookie (registered in Persistence) is default ─
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                // Prevent ASP.NET Core from remapping JWT claim names to WS-Federation URIs.
                // Without this, "sub" gets renamed to ClaimTypes.NameIdentifier and
                // GetUserId() (which searches for JwtRegisteredClaimNames.Sub = "sub") returns null.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ClockSkew = TimeSpan.Zero,
                };

                // When the JWT authentication fails for a browser/navigation request, redirect
                // the user to the login page instead of returning a JSON 401 response. For
                // API calls (Accept: application/json) keep the default behavior.
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        // If response already started, do nothing.
                        if (context.Response.HasStarted)
                            return Task.CompletedTask;

                        // Inspect Accept header to decide if we should redirect (HTML navigation)
                        string accept = context.Request.Headers["Accept"].ToString();
                        if (!string.IsNullOrEmpty(accept) && accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                        {
                            string returnUrl = context.Request.Path + context.Request.QueryString;
                            context.Response.Redirect($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
                            context.HandleResponse(); // suppress default 401
                        }

                        return Task.CompletedTask;
                    },

                    // The one observable half of "oturum bayatladı" (see AuthEventType.
                    // SessionExpired's own doc comment): the browser still had the token
                    // (the server-side session hadn't idle-evicted it yet), but the
                    // token's own expiry had passed. Reads the claims without
                    // re-validating — the token already failed validation, so this is
                    // only ever used for the audit label, never for an auth decision.
                    OnAuthenticationFailed = async context =>
                    {
                        if (context.Exception is not SecurityTokenExpiredException)
                            return;

                        string authHeader = context.Request.Headers.Authorization.ToString();
                        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                            return;

                        try
                        {
                            var handler = new JwtSecurityTokenHandler();
                            string raw = authHeader["Bearer ".Length..].Trim();
                            if (!handler.CanReadToken(raw))
                                return;

                            JwtSecurityToken expired = handler.ReadJwtToken(raw);
                            string? userIdClaim = expired.Claims
                                .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
                            string userName = expired.Claims
                                .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.UniqueName)?.Value
                                ?? "(bilinmiyor)";

                            IAuthEventLogger authEventLogger =
                                context.HttpContext.RequestServices.GetRequiredService<IAuthEventLogger>();
                            await authEventLogger.LogAsync(
                                AuthEventType.SessionExpired,
                                Guid.TryParse(userIdClaim, out Guid userId) ? userId : null,
                                userName,
                                cancellationToken: context.HttpContext.RequestAborted);
                        }
                        catch (Exception)
                        {
                            // Best-effort audit logging — never let a broken/unreadable
                            // token turn into an unhandled exception in the auth pipeline.
                        }
                    },
                };
            });

        // Register middleware that copies JWT from session into Authorization header
        services.AddScoped<JwtFromSessionMiddleware>();
        services.AddScoped<IAuthEventLogger, AuthEventLogger>();

        // ── Authorization ─────────────────────────────────────────────────────────
        // Require authenticated user by default for all endpoints/components. Specific
        // endpoints can opt-out using [AllowAnonymous] when needed.
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .Build();
        });

        // ── Token provider ────────────────────────────────────────────────────────
        services.AddScoped<ITokenProvider, JwtTokenProvider>();
        services.AddScoped<IPreviewLinkSigner, PreviewLinkSigner>();
        services.AddSingleton<IIntegrationSecretCrypto, DataProtectionSecretCrypto>();

        // ── User context ──────────────────────────────────────────────────────────
        // CurrentUserPrincipalHolder is a scoped in-memory store for the current
        // user's ClaimsPrincipal.  In Blazor Server the SignalR circuit does not
        // re-run the HTTP middleware, so IHttpContextAccessor is unreliable.
        // AdminLayout populates the holder from AuthenticationStateProvider;
        // UserContext reads it as a fallback when HttpContext is unavailable.
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUserPrincipalHolder>();
        services.AddScoped<IUserContext, UserContext>();

        // Live role→permission snapshot, so revoking a permission takes effect for
        // already-signed-in users instead of waiting for their next sign-in.
        services.AddSingleton<IRolePermissionCache, RolePermissionCache>();
        services.AddHostedService<RolePermissionCacheWarmup>();
        services.AddSpectraUtilsScoped();
        // ── Email ─────────────────────────────────────────────────────────────────
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IEmailService, EmailService>();

        // ── File storage ──────────────────────────────────────────────────────────
        services.AddOptions<FileServiceOptions>()
            .Bind(configuration.GetSection(FileServiceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IFaviconService, FaviconService>();

        // ── Object storage — pluggable: local disk (default) or S3(-compatible).
        // Which provider is active is set from ObjectStorage:Provider (appsettings.json
        // or the CMS's "Sırlar" screen — see IntegrationSecretsBootstrap) and, unlike
        // most infrastructure topology choices, is read LIVE rather than picked once
        // here: both implementations stay registered, and IBlobStorageFactory resolves
        // whichever one the current setting names on every call, so a Sırlar save
        // (including switching providers) applies with no restart. See
        // IS3ClientProvider for how S3 credential changes rebuild the underlying client.
        services.AddOptions<ObjectStorageOptions>()
            .Bind(configuration.GetSection(ObjectStorageOptions.SectionName));
        services.AddSingleton<IS3ClientProvider, S3ClientProvider>();
        services.AddScoped<S3BlobStorage>();
        services.AddScoped<LocalDiskBlobStorage>();
        services.AddSingleton<IBlobStorageFactory, BlobStorageFactory>();
        services.AddScoped<IBlobStorage>(sp => sp.GetRequiredService<IBlobStorageFactory>().Create(sp));

        // ── Sitemap job (registered with Hangfire in the host project) ─────────────
        services.AddScoped<SitemapJob>();
        services.AddScoped<ISitemapRegenerator, HangfireSitemapRegenerator>();
        services.AddScoped<Files.ImageDerivativesBackfillJob>();

        // ── Database backup (recurring job wired up in the host project) ──────────
        // Everything is executed BY SQL Server, so no filesystem permission setup is
        // needed as long as Backup:Directory stays empty (see BackupOptions).
        services.Configure<BackupOptions>(configuration.GetSection(BackupOptions.SectionName));
        services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();
        services.AddScoped<IBackupJobLauncher, HangfireBackupJobLauncher>();
        services.AddScoped<DatabaseBackupJob>();

        // ── Telemetry retention (recurring job wired up in the host project) ──────
        services.Configure<RetentionOptions>(configuration.GetSection(RetentionOptions.SectionName));
        services.AddScoped<DataRetentionJob>();

        // ── Cache clear — calls the Web app's /api/cache/clear (see CacheClearService) ──
        services.Configure<CacheClearOptions>(configuration.GetSection(CacheClearOptions.SectionName));
        services.AddHttpClient<ICacheClearService, CacheClearService>();
        services.AddSingleton<IPublicSiteCacheInvalidator, PublicSiteCacheInvalidator>();

        // ── Integration secrets reload — calls the Web app's /api/secrets/reload
        // after a "Sırlar" save touches a Captcha:* key (see IntegrationSecretsChangeNotifier).
        // Reuses CacheClearOptions rather than a third shared secret/base-URL pair.
        services.AddHttpClient<IIntegrationSecretsChangeNotifier, IntegrationSecretsChangeNotifier>();

        // ── Public site reachability, for the header's health indicator ──
        // The cache is a singleton so every editor's header shares one probe result;
        // the probe itself is scoped because it reads a Site Setting.
        services.AddSingleton<PublicSiteProbeCache>();
        services.AddHttpClient<IPublicSiteProbe, PublicSiteProbe>()
            // The probe applies its own 5s budget per attempt; this ceiling only
            // stops a pathological socket from outliving it.
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

        // ── CDN reachability, for the "test" button on Site Settings ──
        // Uncached and on-demand only, unlike the health probe above: it exists to
        // answer for the value an operator just typed.
        services.AddHttpClient<ICdnProbe, CdnProbe>()
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(20));

        return services;
    }
}
