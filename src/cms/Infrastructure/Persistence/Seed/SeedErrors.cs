using SharedKernel.Concrete;

namespace Persistence.Seed;

/// <summary>Failures while seeding a fresh database.</summary>
internal static class SeedErrors
{
    public static Error ImageMissing(string resourceName) =>
        Error.Failure("Seed.ImageMissing", $"Embedded resource '{resourceName}' not found.");
}
