using Application.Abstraction.Data;
using Application.Services;
using Domain.Entities.PublicLanguages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Data;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Verifies the in-memory language snapshot that backs routing: which codes are
/// valid URL prefixes and which language is the (prefix-free) default.
/// </summary>
public sealed class LanguageDirectoryTests
{
    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();

    private LanguageDirectory CreateDirectory()
    {
        ServiceCollection services = new();
        services.AddScoped<IPublicReadDbContext>(_ => TestDbFactory.Create(_options));
        ServiceProvider provider = services.BuildServiceProvider();
        return new LanguageDirectory(provider.GetRequiredService<IServiceScopeFactory>());
    }

    private void Seed(params PublicLanguage[] languages)
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        db.Languages.AddRange(languages);
        db.SaveChanges();
    }

    [Fact]
    public void Before_first_refresh_only_the_tr_fallback_is_known()
    {
        LanguageDirectory directory = CreateDirectory();

        directory.DefaultLanguageCode.ShouldBe("tr");
        directory.IsKnownLanguageCode("tr").ShouldBeTrue();
        directory.IsKnownLanguageCode("en").ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_adopts_the_cms_language_set_and_default_flag()
    {
        Seed(
            new PublicLanguage { Id = 1, TwoLetterCode = "tr", IsDefault = false, IsActive = true, IsPublished = true },
            new PublicLanguage { Id = 2, TwoLetterCode = "en", IsDefault = true, IsActive = true, IsPublished = true },
            new PublicLanguage { Id = 3, TwoLetterCode = "de", IsDefault = false, IsActive = false });

        LanguageDirectory directory = CreateDirectory();
        await directory.RefreshAsync();

        directory.DefaultLanguageCode.ShouldBe("en");
        directory.IsKnownLanguageCode("tr").ShouldBeTrue();
        directory.IsKnownLanguageCode("en").ShouldBeTrue();
        directory.IsKnownLanguageCode("de").ShouldBeFalse(); // inactive languages are not routable
    }

    [Fact]
    public async Task An_active_but_unpublished_language_is_not_routable()
    {
        // The whole point of the two flags being separate: a translator can work in
        // German for weeks with the language active in the CMS, and nothing about it
        // reaches the public site until someone publishes it.
        Seed(
            new PublicLanguage { Id = 1, TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true },
            new PublicLanguage { Id = 2, TwoLetterCode = "de", IsDefault = false, IsActive = true, IsPublished = false });

        LanguageDirectory directory = CreateDirectory();
        await directory.RefreshAsync();

        directory.IsKnownLanguageCode("tr").ShouldBeTrue();
        directory.IsKnownLanguageCode("de").ShouldBeFalse();
    }

    [Fact]
    public async Task A_published_but_deactivated_language_is_not_routable_either()
    {
        // The reverse case, and the one that matters when a language is pulled:
        // deactivating it must take it off the site, not leave it half-live.
        Seed(
            new PublicLanguage { Id = 1, TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true },
            new PublicLanguage { Id = 2, TwoLetterCode = "fr", IsDefault = false, IsActive = false, IsPublished = true });

        LanguageDirectory directory = CreateDirectory();
        await directory.RefreshAsync();

        directory.IsKnownLanguageCode("fr").ShouldBeFalse();
    }

    [Fact]
    public async Task Language_code_lookup_is_case_insensitive()
    {
        Seed(new PublicLanguage { Id = 1, TwoLetterCode = "en", IsDefault = true, IsActive = true, IsPublished = true });

        LanguageDirectory directory = CreateDirectory();
        await directory.RefreshAsync();

        directory.IsKnownLanguageCode("EN").ShouldBeTrue();
        directory.IsKnownLanguageCode("En").ShouldBeTrue();
    }

    [Fact]
    public async Task Refresh_against_an_empty_table_keeps_the_previous_snapshot()
    {
        LanguageDirectory directory = CreateDirectory();
        await directory.RefreshAsync();

        directory.DefaultLanguageCode.ShouldBe("tr");
        directory.IsKnownLanguageCode("tr").ShouldBeTrue();
    }
}
