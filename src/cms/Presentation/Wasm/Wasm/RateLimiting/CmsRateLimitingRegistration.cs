using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Wasm.RateLimiting;

/// <summary>
/// Rate limiting for the admin panel. Only the anonymous surface is limited — see
/// <see cref="CmsRateLimitOptions"/> for why an authenticated editor's normal work
/// deliberately is not.
/// </summary>
public static class CmsRateLimitingRegistration
{
    /// <summary>Policy applied to the sign-in endpoint.</summary>
    public const string LoginPolicy = "cms-login";

    public static IServiceCollection AddCmsRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CmsRateLimitOptions>(configuration.GetSection(CmsRateLimitOptions.SectionName));

        // Read once: the limiter partitions are built at startup, so changing a
        // budget needs a restart either way.
        CmsRateLimitOptions settings = configuration
            .GetSection(CmsRateLimitOptions.SectionName)
            .Get<CmsRateLimitOptions>() ?? new CmsRateLimitOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            if (settings.Enabled)
            {
                limiter.AddPolicy(LoginPolicy, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: $"{LoginPolicy}:{ResolveClientIp(httpContext, settings)}",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = Math.Max(settings.LoginPermitLimit, 1),
                            Window = TimeSpan.FromSeconds(Math.Max(settings.WindowSeconds, 1)),
                            // No queue: an over-budget attempt is refused immediately
                            // rather than parked, which is what stops a flood from
                            // occupying request threads.
                            QueueLimit = 0,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = true,
                        }));
            }
            else
            {
                // The endpoint still declares the policy by name, so it has to resolve
                // even when limiting is switched off.
                limiter.AddPolicy(LoginPolicy, _ => RateLimitPartition.GetNoLimiter("disabled"));
            }

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                string retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retry)
                    ? ((int)Math.Ceiling(retry.TotalSeconds)).ToString(CultureInfo.InvariantCulture)
                    : Math.Max(settings.WindowSeconds, 1).ToString(CultureInfo.InvariantCulture);

                context.HttpContext.Response.Headers.RetryAfter = retryAfter;

                // Logged at warning: repeated hits on this policy are the signature of
                // a password spray, and that is worth seeing in the log.
                ILogger logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(CmsRateLimitingRegistration));
                logger.LogWarning(
                    "Sign-in rate limit rejected a request from {ClientIp}.",
                    ResolveClientIp(context.HttpContext, settings));

                await context.HttpContext.Response.WriteAsync(
                    "Too many sign-in attempts. Please wait and try again.", cancellationToken);
            };
        });

        return services;
    }

    /// <summary>
    /// The bucket key. An unidentifiable caller shares one "unknown" partition rather
    /// than getting an unlimited one — strict on purpose.
    /// </summary>
    private static string ResolveClientIp(HttpContext httpContext, CmsRateLimitOptions settings)
    {
        if (settings.TrustForwardedForHeader)
        {
            string forwarded = httpContext.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                // The header is a chain ("client, proxy1, proxy2"); the original
                // client is the first entry.
                string first = forwarded.Split(',')[0].Trim();
                if (first.Length > 0)
                    return first;
            }
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
