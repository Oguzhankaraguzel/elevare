using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Data;

namespace Persistence.DependencyInjection;

public static class PersistenceServiceRegistration
{
    /// <summary>
    /// Registers a read-only connection to the CMS's database. The Web app never
    /// writes to it and never runs migrations against it — the CMS owns the schema.
    /// </summary>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");

        // Retry on transient faults. This is not only about flaky networks: the site
        // and the CMS come up together, and the CMS is what creates the schema — so
        // on a first run the site will legitimately meet a database that is still
        // being built. Retrying turns that race into a slow first request rather
        // than a page of 500s.
        services.AddDbContext<PublicReadDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));

        services.AddScoped<IPublicReadDbContext>(sp => sp.GetRequiredService<PublicReadDbContext>());

        // Append-only telemetry (PageViewHits) — the one table the Web app writes to.
        services.AddDbContext<AnalyticsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));

        services.AddScoped<IAnalyticsDbContext>(sp => sp.GetRequiredService<AnalyticsDbContext>());

        return services;
    }
}
