using Application.Abstraction.Services.Authentication;
using Domain.Entities.Languages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Persistence.Data;
using Persistence.Seed;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// What someone gets on the very first start, before they have touched anything.
/// <para>
/// A language needs both flags to reach a visitor: <c>IsActive</c> to be authored in,
/// <c>IsPublished</c> to be served. The seeder set only the first, so a brand-new
/// install had no publicly visible language at all — the admin could write a home
/// page, publish it, turn maintenance mode off, and still be served nothing, with
/// no error anywhere to explain it. Found by installing the project from scratch.
/// </para>
/// </summary>
public sealed class FreshInstallSeedTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            // ApplicationDbContext wraps SaveChanges in a transaction, which the
            // InMemory provider cannot honour.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private async Task<List<Language>> SeedAsync()
    {
        using ApplicationDbContext db = new(_options, new SeedTestUserContext());
        await DatabaseSeeder.SeedLanguagesAsync(db, NullLogger.Instance, CancellationToken.None);
        return await db.Languages.ToListAsync();
    }

    [Fact]
    public async Task The_default_language_is_published_so_a_new_site_can_serve_something()
    {
        List<Language> languages = await SeedAsync();

        Language? defaultLanguage = languages.SingleOrDefault(l => l.IsDefault);

        defaultLanguage.ShouldNotBeNull("a fresh install must have exactly one default language");
        defaultLanguage!.IsActive.ShouldBeTrue();
        defaultLanguage.IsPublished.ShouldBeTrue(
            "an unpublished default language makes every page invisible with nothing to explain why");
    }

    [Fact]
    public async Task Every_seeded_language_is_both_active_and_published()
    {
        List<Language> languages = await SeedAsync();

        languages.ShouldNotBeEmpty();
        languages.ShouldAllBe(l => l.IsActive && l.IsPublished);
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_the_languages()
    {
        await SeedAsync();

        using ApplicationDbContext db = new(_options, new SeedTestUserContext());
        await DatabaseSeeder.SeedLanguagesAsync(db, NullLogger.Instance, CancellationToken.None);

        // Every deploy runs the seeder again; a second pass must be a no-op.
        (await db.Languages.CountAsync()).ShouldBe(2);
    }

    private sealed class SeedTestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
