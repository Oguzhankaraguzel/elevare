using Application.Features.Queries.SiteSettings.GetPublicSiteSettings;
using Domain.Entities.PublicSiteSettings;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Features;

public sealed class GetPublicSiteSettingsQueryHandlerTests
{
    [Fact]
    public async Task Returns_key_value_map_excluding_soft_deleted_settings()
    {
        DbContextOptions<PublicReadDbContext> options = TestDbFactory.CreateOptions();
        using (PublicReadDbContext seedDb = TestDbFactory.Create(options))
        {
            seedDb.SiteSettings.AddRange(
                new PublicSiteSetting { Id = 1, Key = "General.SiteName", Value = "Elevare" },
                new PublicSiteSetting { Id = 2, Key = "Appearance.LogoUrl", Value = null },
                new PublicSiteSetting { Id = 3, Key = "Old.Removed", Value = "x", IsDeleted = true });
            await seedDb.SaveChangesAsync(CancellationToken.None);
        }

        using PublicReadDbContext db = TestDbFactory.Create(options);
        GetPublicSiteSettingsQueryHandler handler = new(db, new NoOpCacheService());

        Result<Dictionary<string, string?>> result =
            await handler.Handle(new GetPublicSiteSettingsQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value["General.SiteName"].ShouldBe("Elevare");
        result.Value.ContainsKey("Appearance.LogoUrl").ShouldBeTrue();
        result.Value["Appearance.LogoUrl"].ShouldBeNull();
        result.Value.ContainsKey("Old.Removed").ShouldBeFalse();
    }
}
