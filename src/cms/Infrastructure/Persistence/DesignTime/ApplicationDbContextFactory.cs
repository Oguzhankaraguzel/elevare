using Application.Abstraction.Services.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Persistence.Data;

namespace Persistence.DesignTime;

/// <summary>
/// Used only by <c>dotnet ef</c> (migrations, database update) — never at runtime.
/// <para>
/// Resolves the connection the same way the running app does, so pointing the
/// tooling at another database is just an environment variable:
/// <c>ConnectionStrings__DefaultConnection=... dotnet ef database update</c>.
/// It used to hard-code one machine's server name, which silently sent every
/// migration to that one database no matter what the caller intended.
/// </para>
/// </summary>
internal sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string LocalFallback =
        "Host=localhost;Port=5432;Database=ElevareDB;Username=postgres;Password=postgres;";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString =
            configuration.GetConnectionString("DefaultConnection") is { Length: > 0 } configured
                ? configured
                : LocalFallback;

        DbContextOptionsBuilder<ApplicationDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name));

        return new ApplicationDbContext(optionsBuilder.Options, new DesignTimeUserContext());
    }

    private sealed class DesignTimeUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => false;
        public bool CanAuthorCustomCode => false;
        public bool HasPermission(string permissionKey) => false;
        public bool IsInRole(string roleName) => false;
    }
}
