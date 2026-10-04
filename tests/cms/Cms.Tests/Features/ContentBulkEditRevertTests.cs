using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.ContentBulkEdits.ApplyContentBulkEdit;
using Application.Features.Commands.ContentBulkEdits.RevertContentBulkEdit;
using Domain.Entities.ContentBulkEdits;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>A find/replace across pages can be undone, once.</summary>
public sealed class ContentBulkEditRevertTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new User());

    private async Task<int> ApplyAsync()
    {
        using (ApplicationDbContext seed = CreateDb())
        {
            seed.Languages.Add(new Language { Id = 1, NameInNative = "Türkçe", NameInEnglish = "Turkish", TwoLetterCode = "tr", IsDefault = true, IsActive = true, IsPublished = true });
            seed.PageInfos.Add(new PageInfo { Id = 1, Slug = "a", FullSlug = "a", LanguageId = 1, PageStatus = PageStatus.Published,
                SeoMeta = new SeoMeta { Title = "A" }, Content = new PageContent { GjsHtml = "<p>Eski firma adı</p>", GjsData = "{\"t\":\"Eski firma adı\"}" } });
            await seed.SaveChangesAsync();
        }

        using ApplicationDbContext db = CreateDb();
        Result<ApplyContentBulkEditResponse> result = await new ApplyContentBulkEditCommandHandler(db).Handle(
            new ApplyContentBulkEditCommand("Eski firma", "Yeni firma", [1]), CancellationToken.None);
        result.Value.AffectedPageCount.ShouldBe(1);
        return result.Value.ContentBulkEditId;
    }

    private async Task<Result> RevertAsync(int id)
    {
        using ApplicationDbContext db = CreateDb();
        Result result = await new RevertContentBulkEditCommandHandler(db).Handle(new RevertContentBulkEditCommand(id), CancellationToken.None);
        await db.SaveChangesAsync();
        return result;
    }

    [Fact]
    public async Task Reverting_puts_every_affected_page_back()
    {
        int id = await ApplyAsync();

        (await RevertAsync(id)).IsSuccess.ShouldBeTrue();

        using ApplicationDbContext read = CreateDb();
        PageContent content = await read.PageContents.SingleAsync();
        content.GjsHtml.ShouldBe("<p>Eski firma adı</p>");
        content.GjsData.ShouldBe("{\"t\":\"Eski firma adı\"}");
    }

    [Fact]
    public async Task A_run_cannot_be_reverted_twice()
    {
        int id = await ApplyAsync();
        (await RevertAsync(id)).IsSuccess.ShouldBeTrue();

        (await RevertAsync(id)).Error.ShouldBe(ContentBulkEditErrors.AlreadyReverted);
    }

    private sealed class User : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
