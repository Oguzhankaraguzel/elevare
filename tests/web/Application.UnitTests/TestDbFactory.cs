using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.UnitTests;

/// <summary>
/// Creates the REAL <see cref="PublicReadDbContext"/> (same model configuration,
/// same query filters) on the EF InMemory provider, so handler tests exercise the
/// production mapping instead of a hand-rolled fake. Reuse the returned options to
/// open additional contexts over the same store within a test.
/// </summary>
internal static class TestDbFactory
{
    public static DbContextOptions<PublicReadDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<PublicReadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    public static PublicReadDbContext Create(DbContextOptions<PublicReadDbContext> options) => new(options);
}
