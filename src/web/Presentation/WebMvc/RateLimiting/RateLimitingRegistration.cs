using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace WebMvc.RateLimiting;

/// <summary>
/// Wires up the public site's rate limiting. Only the API endpoints are limited —
/// see <see cref="RateLimitPolicies"/> for why page rendering deliberately is not.
/// </summary>
public static class RateLimitingRegistration
{
    public static IServiceCollection AddElevareRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        // Read once here (rather than through IOptionsMonitor) because the limiter
        // partitions are built at startup; changing a budget needs a restart.
        RateLimitOptions settings = configuration
            .GetSection(RateLimitOptions.SectionName)
            .Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            if (settings.Enabled)
            {
                int window = Math.Max(settings.WindowSeconds, 1);
                AddIpPolicy(limiter, RateLimitPolicies.FormSubmit, settings.FormSubmitPermitLimit, window, settings);
                AddIpPolicy(limiter, RateLimitPolicies.Search, settings.SearchPermitLimit, window, settings);
                AddIpPolicy(limiter, RateLimitPolicies.Telemetry, settings.TelemetryPermitLimit, window, settings);
                AddIpPolicy(limiter, RateLimitPolicies.ClientLog, settings.ClientLogPermitLimit, window, settings);
                AddIpPolicy(limiter, RateLimitPolicies.CacheAdmin, settings.CacheAdminPermitLimit, window, settings);
            }
            else
            {
                // The endpoints still declare RequireRateLimiting(...), so the policy
                // names must resolve even when limiting is switched off — register
                // pass-through policies instead of removing the attributes.
                foreach (string policy in AllPolicies)
                    limiter.AddPolicy(policy, _ => RateLimitPartition.GetNoLimiter("disabled"));
            }

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                // Tell well-behaved callers when to come back, and make the reason
                // explicit in the body so a 429 is never mistaken for a bug.
                string retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retry)
                    ? ((int)Math.Ceiling(retry.TotalSeconds)).ToString(CultureInfo.InvariantCulture)
                    : Math.Max(settings.WindowSeconds, 1).ToString(CultureInfo.InvariantCulture);

                context.HttpContext.Response.Headers.RetryAfter = retryAfter;

                ILogger logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(RateLimitingRegistration));
                logger.LogWarning(
                    "Rate limit rejected {Method} {Path} from {ClientIp}.",
                    context.HttpContext.Request.Method,
                    context.HttpContext.Request.Path.Value,
                    ResolveClientIp(context.HttpContext, settings));

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new RateLimitRejectionResponse(
                        "Too many requests. Please slow down and try again shortly.",
                        int.Parse(retryAfter, CultureInfo.InvariantCulture)),
                    cancellationToken);
            };
        });

        return services;
    }

    private static readonly string[] AllPolicies =
    [
        RateLimitPolicies.FormSubmit,
        RateLimitPolicies.Search,
        RateLimitPolicies.Telemetry,
        RateLimitPolicies.ClientLog,
        RateLimitPolicies.CacheAdmin
    ];

    /// <summary>
    /// A fixed window per client IP, with no queue — an over-budget request is
    /// rejected immediately rather than held open, which is what keeps a flood from
    /// consuming request threads (the whole point of limiting it).
    /// </summary>
    private static void AddIpPolicy(
        RateLimiterOptions limiter, string policyName, int permitLimit, int windowSeconds, RateLimitOptions settings) =>
        limiter.AddPolicy(policyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"{policyName}:{ResolveClientIp(httpContext, settings)}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(permitLimit, 1),
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                }));

    /// <summary>
    /// The bucket key. Falls back to a single shared "unknown" partition when no IP
    /// is available — deliberately strict: an unidentifiable caller shares a bucket
    /// rather than getting an unlimited one.
    /// </summary>
    private static string ResolveClientIp(HttpContext httpContext, RateLimitOptions settings)
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
