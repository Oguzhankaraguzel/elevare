using Application.Abstraction.Data;
using Application.Services;
using Domain.Entities.PublicSiteSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Data;
using Shouldly;

namespace Application.UnitTests.Services;

public sealed class MaintenanceStateTests
{
    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();

    private MaintenanceState CreateState()
    {
        ServiceCollection services = new();
        services.AddScoped<IPublicReadDbContext>(_ => TestDbFactory.Create(_options));
        ServiceProvider provider = services.BuildServiceProvider();
        return new MaintenanceState(provider.GetRequiredService<IServiceScopeFactory>());
    }

    private void SeedSetting(string? value)
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        db.SiteSettings.Add(new PublicSiteSetting { Id = 1, Key = MaintenanceState.SettingKey, Value = value });
        db.SaveChanges();
    }

    [Fact]
    public async Task Enabled_when_the_setting_is_true()
    {
        SeedSetting("true");
        MaintenanceState state = CreateState();

        await state.RefreshAsync();

        state.IsEnabled.ShouldBeTrue();
    }

    [Theory]
    [InlineData("false")]
    [InlineData(null)]
    [InlineData("garbage")]
    public async Task Disabled_for_anything_other_than_true(string? value)
    {
        SeedSetting(value);
        MaintenanceState state = CreateState();

        await state.RefreshAsync();

        state.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Disabled_when_the_setting_row_does_not_exist()
    {
        MaintenanceState state = CreateState();

        await state.RefreshAsync();

        state.IsEnabled.ShouldBeFalse();
    }
}
