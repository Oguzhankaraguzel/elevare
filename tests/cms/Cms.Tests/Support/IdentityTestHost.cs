using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Email;
using Domain.Entities.Users;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Data;

namespace Cms.Tests.Support;

/// <summary>
/// A real <see cref="UserManager{TUser}"/> over an in-memory database, set up with the
/// same Identity options as the application (password policy, unique e-mail, token
/// providers) — so the account commands are tested against what they actually call,
/// not a mock of it.
/// </summary>
internal sealed class IdentityTestHost : IDisposable
{
    private readonly ServiceProvider _services;

    public IdentityTestHost()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<IUserContext>(CurrentUser);
        services.AddSingleton<IEmailService>(Emails);

        string database = Guid.NewGuid().ToString("N");
        services.AddDbContext<ApplicationDbContext>(o => o
            .UseInMemoryDatabase(database)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<ICmsApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // Mirrors PersistenceServiceRegistration.
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        _services = services.BuildServiceProvider();
    }

    public SettableUserContext CurrentUser { get; } = new();

    public RecordingEmailService Emails { get; } = new();

    /// <summary>A fresh scope — one request's worth of DbContext and UserManager.</summary>
    public IServiceScope Scope() => _services.CreateScope();

    public void Dispose() => _services.Dispose();
}
