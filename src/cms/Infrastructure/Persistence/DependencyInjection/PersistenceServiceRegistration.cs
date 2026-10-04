using Application.Abstraction.Data;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Data;
using Persistence.Seed;

namespace Persistence.DependencyInjection;

public static class PersistenceServiceRegistration
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");

        // ── DbContext ─────────────────────────────────────────────────────────────
        services.AddDbContext<ApplicationDbContext>(options =>
            // Deliberately NO EnableRetryOnFailure here, unlike the public site.
            // ApplicationDbContext opens its own transaction around SaveChanges, and a
            // retrying execution strategy refuses to run inside a user-initiated one —
            // the app then throws on the very first save and never finishes starting.
            // Adding it back means routing every save through
            // Database.CreateExecutionStrategy().ExecuteAsync(...) first.
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name)));

        // Expose the context through the abstraction so Application layer stays DB-agnostic.
        services.AddScoped<ICmsApplicationDbContext>(
            sp => sp.GetRequiredService<ApplicationDbContext>());

        // A second way to reach the database, for the rare handler that must NOT be
        // serialized behind the per-circuit concurrency gate the line above's shared
        // instance requires (see IBypassDbConcurrencyGuard) — each call gets its own,
        // independent context built straight from the SAME DbContextOptions<T> the
        // registration above already put in the container.
        //
        // Not EF Core's AddDbContextFactory, for a reason that is structural rather
        // than a preference: IDbContextFactory<T> is generic over the concrete
        // context, and ApplicationDbContext is internal to this assembly, which the
        // Application project does not reference at all. A handler over there could
        // not name the type, so an abstraction owned by Application is required
        // either way — and once it exists, EF's factory would only sit behind it
        // doing what the one constructor call below already does.
        //
        // Scoped, not singleton: ApplicationDbContext takes IUserContext, which is
        // itself scoped. (EF's factory defaults to a singleton and would need the
        // same override, for the same reason.)
        //
        // Pooling is separately off the table — AddPooledDbContextFactory reuses
        // instances, and pooling this specific context has already caused a
        // production-only bug once; dotnet build/test never exercise a real DI
        // container, so that class of problem surfaces only live, the same way the
        // Redis connection bug once did. See CHANGELOG.
        services.AddScoped<ICmsApplicationDbContextFactory, CmsApplicationDbContextFactory>();

        // ── ASP.NET Core Identity ─────────────────────────────────────────────────
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;

                // Brute-force protection, per account. The columns for this have
                // always been in the schema and AuthErrors.AccountLocked was already
                // written — what was missing was the policy and, in the login
                // handler, the calls that read and feed it.
                // Five attempts then a fifteen-minute pause: an attacker gets
                // ~20 guesses an hour, a person who mistypes twice notices nothing.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<AppRole>()
            .AddSignInManager<SignInManager<AppUser>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // ── First-run accounts ────────────────────────────────────────────────────
        // Bound but not validated on start: an already-seeded database needs no
        // Seed section at all, and refusing to boot over a missing one would break
        // every existing deployment. DatabaseSeeder warns when it matters.
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        // ── Health checks ─────────────────────────────────────────────────────────
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["db", "sql"]);

        return services;
    }
}
