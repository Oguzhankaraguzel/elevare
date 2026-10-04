using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Pages.CreatePageTranslation;
using Application.Features.Commands.Pages.DeletePage;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// Deleting a translation and creating it again is an ordinary editing mistake —
/// wrong language picked, page started over. It has to be repeatable, so these
/// tests walk the full create → delete → create-again cycle against the real
/// handlers and the real model configuration (soft-delete query filter included).
/// </summary>
public sealed class CreatePageTranslationAfterDeleteTests
{
    private const int TurkishId = 1;
    private const int EnglishId = 2;

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            // ApplicationDbContext wraps SaveChanges in a transaction, which the
            // InMemory provider cannot honour.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new TranslationTestUserContext());

    private async Task<int> SeedSourcePageAsync(string slug)
    {
        using ApplicationDbContext db = CreateDb();
        var turkish = new Language
        {
            Id = TurkishId,
            NameInNative = "Türkçe",
            NameInEnglish = "Turkish",
            TwoLetterCode = "tr",
            IsDefault = true,
            IsActive = true,
            IsPublished = true,
        };
        db.Languages.AddRange(
            turkish,
            new Language
            {
                Id = EnglishId,
                NameInNative = "English",
                NameInEnglish = "English",
                TwoLetterCode = "en",
                IsActive = true,
                IsPublished = true,
            });

        var page = new PageInfo
        {
            Slug = slug,
            PageStatus = PageStatus.Published,
            LanguageId = TurkishId,
            // The navigation, not just the id: ComputeFullSlug reads Language directly
            // and a not-yet-saved entity cannot resolve it from LanguageId alone.
            Language = turkish,
            SeoMeta = new SeoMeta { Title = slug },
            Content = new PageContent(),
        };
        page.ComputeFullSlug("tr");
        db.PageInfos.Add(page);
        await db.SaveChangesAsync(CancellationToken.None);
        return page.Id;
    }

    private async Task<Result<int>> CreateTranslationAsync(int sourceId)
    {
        using ApplicationDbContext db = CreateDb();
        return await new CreatePageTranslationCommandHandler(db)
            .Handle(new CreatePageTranslationCommand(sourceId, EnglishId), CancellationToken.None);
    }

    private async Task<Result> DeleteAsync(int pageId)
    {
        using ApplicationDbContext db = CreateDb();
        Result result = await new DeletePageCommandHandler(db)
            .Handle(new DeletePageCommand(pageId, null), CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
        return result;
    }

    [Fact]
    public async Task A_deleted_translation_can_be_created_again()
    {
        int sourceId = await SeedSourcePageAsync("maintenance");

        Result<int> first = await CreateTranslationAsync(sourceId);
        first.IsSuccess.ShouldBeTrue();

        (await DeleteAsync(first.Value)).IsSuccess.ShouldBeTrue();

        Result<int> second = await CreateTranslationAsync(sourceId);

        second.IsSuccess.ShouldBeTrue(
            $"recreating a deleted translation must work, but it failed with '{(second.IsFailure ? second.Error.Code : "")}'");
    }

    [Fact]
    public async Task The_recreated_translation_keeps_the_original_slug()
    {
        // The slug-collision check must ignore the deleted row too. Otherwise every
        // delete/recreate cycle appends another language suffix and the URL drifts:
        // maintenance -> maintenance-en -> maintenance-en-en.
        int sourceId = await SeedSourcePageAsync("maintenance");

        Result<int> first = await CreateTranslationAsync(sourceId);
        await DeleteAsync(first.Value);
        Result<int> second = await CreateTranslationAsync(sourceId);

        using ApplicationDbContext db = CreateDb();
        PageInfo recreated = await db.PageInfos.SingleAsync(p => p.Id == second.Value, CancellationToken.None);

        recreated.Slug.ShouldBe("maintenance");
        recreated.FullSlug.ShouldBe("en/maintenance");
    }

    [Fact]
    public async Task A_live_translation_still_blocks_a_second_one()
    {
        // The guard must not be loosened into uselessness: while a translation is
        // actually there, asking for another one is still an error.
        int sourceId = await SeedSourcePageAsync("hakkimizda");

        (await CreateTranslationAsync(sourceId)).IsSuccess.ShouldBeTrue();
        Result<int> duplicate = await CreateTranslationAsync(sourceId);

        duplicate.IsFailure.ShouldBeTrue();
        duplicate.Error.Code.ShouldBe(PageInfoErrors.TranslationAlreadyExists.Code);
    }

    private sealed class TranslationTestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
