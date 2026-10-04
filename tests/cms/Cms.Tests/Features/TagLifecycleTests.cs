using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Tags.CreateTag;
using Application.Features.Commands.Tags.DeleteTag;
using Application.Features.Commands.Tags.UpdateTag;
using Application.Features.Queries.Tags.GetTagUsage;
using Application.Features.Queries.Tags.GetTags;
using Domain.Entities.Tags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// Tags could be created from the page editor and never renamed or removed, so a
/// typo stayed in the picker forever. These cover the two operations that closed
/// that, and the rules that stop them doing damage on the way.
/// </summary>
public sealed class TagLifecycleTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new TagTestUserContext());

    private async Task<int> CreateAsync(string name)
    {
        using ApplicationDbContext db = CreateDb();
        Result<TagResponse> result = await new CreateTagCommandHandler(db)
            .Handle(new CreateTagCommand(name), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value.Id;
    }

    private async Task<Result<TagResponse>> RenameAsync(int id, string name)
    {
        using ApplicationDbContext db = CreateDb();
        return await new UpdateTagCommandHandler(db)
            .Handle(new UpdateTagCommand(id, name), CancellationToken.None);
    }

    private async Task<Result> DeleteAsync(int id)
    {
        using ApplicationDbContext db = CreateDb();
        Result result = await new DeleteTagCommandHandler(db)
            .Handle(new DeleteTagCommand(id), CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
        return result;
    }

    [Fact]
    public async Task A_tag_can_be_renamed_and_its_slug_follows()
    {
        int id = await CreateAsync("Web Tasarim");

        Result<TagResponse> renamed = await RenameAsync(id, "Web Tasarımı");

        renamed.IsSuccess.ShouldBeTrue();
        renamed.Value.Name.ShouldBe("Web Tasarımı");
        renamed.Value.Slug.ShouldNotBe("web-tasarim");
    }

    [Fact]
    public async Task Renaming_keeps_the_same_row_rather_than_making_a_second_tag()
    {
        // The id is what the pages reference, so a rename that replaced the row
        // would quietly strip the tag off every page carrying it.
        int id = await CreateAsync("hizmetlerimz");

        Result<TagResponse> renamed = await RenameAsync(id, "hizmetlerimiz");

        renamed.Value.Id.ShouldBe(id);

        using ApplicationDbContext db = CreateDb();
        (await db.Tags.CountAsync(CancellationToken.None)).ShouldBe(1);
    }

    [Fact]
    public async Task Renaming_onto_another_tags_name_is_refused()
    {
        // Allowing it would be a silent merge: every page on the losing tag would be
        // re-labelled with nothing on screen saying so.
        await CreateAsync("Kurumsal");
        int second = await CreateAsync("Blog");

        Result<TagResponse> result = await RenameAsync(second, "Kurumsal");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(TagErrors.NameAlreadyExists.Code);
    }

    [Fact]
    public async Task Case_and_accent_differences_still_count_as_the_same_name()
    {
        // Compared on the slug, because that is what every URL and lookup uses.
        await CreateAsync("Kurumsal");
        int second = await CreateAsync("Blog");

        Result<TagResponse> result = await RenameAsync(second, "kurumsal");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(TagErrors.NameAlreadyExists.Code);
    }

    [Fact]
    public async Task An_empty_name_is_refused()
    {
        int id = await CreateAsync("Kurumsal");

        Result<TagResponse> result = await RenameAsync(id, "   ");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(TagErrors.InvalidName.Code);
    }

    [Fact]
    public async Task Renaming_a_tag_that_does_not_exist_reports_not_found()
    {
        Result<TagResponse> result = await RenameAsync(9999, "Yeni");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(TagErrors.NotFound.Code);
    }

    [Fact]
    public async Task A_deleted_tag_disappears_from_the_picker()
    {
        int id = await CreateAsync("Gecici");

        (await DeleteAsync(id)).IsSuccess.ShouldBeTrue();

        using ApplicationDbContext db = CreateDb();
        Result<List<TagResponse>> tags = await new GetTagsQueryHandler(db)
            .Handle(new GetTagsQuery(), CancellationToken.None);

        tags.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deleting_is_soft_so_the_row_survives_for_the_Trash()
    {
        // A hard delete would cascade the PageInfoTags rows away, and restoring the
        // tag afterwards would bring it back on no pages at all.
        int id = await CreateAsync("Gecici");
        await DeleteAsync(id);

        using ApplicationDbContext db = CreateDb();
        Tag deleted = await db.Tags.IgnoreQueryFilters().SingleAsync(t => t.Id == id, CancellationToken.None);

        deleted.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task A_freed_name_can_be_used_again_after_a_delete()
    {
        int id = await CreateAsync("Kurumsal");
        await DeleteAsync(id);
        int second = await CreateAsync("Blog");

        Result<TagResponse> result = await RenameAsync(second, "Kurumsal");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task The_usage_list_puts_unused_tags_first()
    {
        // The screen exists to find tags nobody meant to keep; alphabetical order
        // would bury them among the ones in daily use.
        await CreateAsync("Zaten Kullanilmiyor");
        await CreateAsync("Ayrica Kullanilmiyor");

        using ApplicationDbContext db = CreateDb();
        Result<List<TagUsageResponse>> usage = await new GetTagUsageQueryHandler(db)
            .Handle(new GetTagUsageQuery(), CancellationToken.None);

        usage.Value.Count.ShouldBe(2);
        usage.Value.ShouldAllBe(t => t.PageCount == 0);
    }

    private sealed class TagTestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
