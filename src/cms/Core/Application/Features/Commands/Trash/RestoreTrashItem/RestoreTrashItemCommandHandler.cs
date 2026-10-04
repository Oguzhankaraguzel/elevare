using Application.Abstraction.Data;
using Application.Features.Commands.Pages;
using Application.Features.Trash;
using Domain.Entities.Abstractions;
using Domain.Entities.ContentBulkEdits;
using Domain.Entities.Languages;
using Domain.Entities.Media;
using Domain.Entities.PageInfos;
using Domain.Entities.PageTemplates;
using Domain.Entities.SiteCodeSnippets;
using Domain.Entities.SiteSettings;
using Domain.Entities.UserNotes;
using Domain.Entities.UserReminders;
using Domain.Entities.UserTasks;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Strings;

namespace Application.Features.Commands.Trash.RestoreTrashItem;

internal sealed class RestoreTrashItemCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<RestoreTrashItemCommand>
{
    public Task<Result> Handle(RestoreTrashItemCommand request, CancellationToken cancellationToken) => request.EntityType switch
    {
        TrashEntityType.PageInfo => RestorePageAsync(request.Id, request.OverrideSlug, cancellationToken),
        TrashEntityType.PageTemplate => RestoreSimpleAsync(db.PageTemplates, request.Id, cancellationToken),
        TrashEntityType.MediaFile => RestoreSimpleAsync(db.MediaFiles, request.Id, cancellationToken),
        TrashEntityType.Language => RestoreLanguageAsync(request.Id, cancellationToken),
        TrashEntityType.SiteSetting => RestoreSiteSettingAsync(request.Id, cancellationToken),
        TrashEntityType.UserTask => RestoreSimpleAsync(db.UserTasks, request.Id, cancellationToken),
        TrashEntityType.UserNote => RestoreSimpleAsync(db.UserNotes, request.Id, cancellationToken),
        TrashEntityType.UserReminder => RestoreSimpleAsync(db.UserReminders, request.Id, cancellationToken),
        TrashEntityType.ContentBulkEdit => RestoreSimpleAsync(db.ContentBulkEdits, request.Id, cancellationToken),
        TrashEntityType.SiteCodeSnippet => RestoreSiteCodeSnippetAsync(request.Id, cancellationToken),
        _ => Task.FromResult(Result.Failure(TrashErrors.UnknownType)),
    };

    /// <summary>
    /// Restores a site code snippet, but leaves it switched OFF.
    /// <para>
    /// Every other trashed entity comes back as something you then go and look at.
    /// This one comes back as code that runs in every visitor's browser on the next
    /// page load — an analytics tag, a chat widget, a consent banner. Restoring the
    /// row and re-serving it are two different decisions, and only the first one was
    /// asked for here. So <see cref="SiteCodeSnippet.IsEnabled"/> is forced to false
    /// and the author turns it back on from Site Codes once they have read what they
    /// are re-publishing. It is one click, and it is the click that should be
    /// deliberate.
    /// </para>
    /// <para>
    /// SortOrder is deliberately left alone. Ordering is
    /// <c>OrderBy(SortOrder).ThenBy(Id)</c> in both the admin list and the public
    /// render, so a value that now collides with a sibling still produces a stable,
    /// sensible position — and renumbering the placement would silently move rows
    /// the author never touched.
    /// </para>
    /// </summary>
    private async Task<Result> RestoreSiteCodeSnippetAsync(int id, CancellationToken cancellationToken)
    {
        SiteCodeSnippet? snippet = await db.SiteCodeSnippets.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.IsDeleted, cancellationToken);
        if (snippet is null)
            return Result.Failure(TrashErrors.NotFound);

        snippet.IsEnabled = false;
        snippet.IsDeleted = false;
        snippet.IsActive = true;
        snippet.DeleteDate = null;
        snippet.DeleteUserId = null;
        return Result.Success();
    }

    private async Task<Result> RestorePageAsync(int id, string? overrideSlug, CancellationToken cancellationToken)
    {
        // ComputeFullSlug() below walks ParentPage.ParentPage.… to the root, so a
        // restored page nested two levels deep needs its parent's own parent loaded
        // too, or the recomputed slug comes back silently truncated.
        PageInfo? page = await db.PageInfos.IgnoreQueryFilters()
            .Include(p => p.Language)
            .Include(p => p.ParentPage).ThenInclude(p => p!.ParentPage)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted, cancellationToken);
        if (page is null)
            return Result.Failure(TrashErrors.NotFound);

        string targetSlug = overrideSlug.HasValue() ? overrideSlug!.ToSlug() : page.Slug;
        if (targetSlug.IsNullOrWhiteSpace())
            return Result.Failure(PageInfoErrors.InvalidSlug);

        bool slugTaken = await db.PageInfos
            .AnyAsync(p => p.Id != page.Id && p.Slug == targetSlug && p.LanguageId == page.LanguageId, cancellationToken);
        if (slugTaken)
            return Result.Failure(PageInfoErrors.SlugAlreadyExists);

        page.Slug = targetSlug;
        string defaultCode = await db.Languages
            .Where(l => l.IsDefault)
            .Select(l => l.TwoLetterCode)
            .FirstOrDefaultAsync(cancellationToken) ?? "tr";
        page.ComputeFullSlug(defaultCode);

        page.IsDeleted = false;
        page.IsActive = true;
        page.DeleteDate = null;
        page.DeleteUserId = null;

        // The live page now legitimately owns this slug again — a redirect sitting
        // at the exact same path (created when the page was removed) is superseded.
        await RedirectResolution.RemoveForRestoredPageAsync(db, page.FullSlug, cancellationToken);

        return Result.Success();
    }

    private async Task<Result> RestoreLanguageAsync(int id, CancellationToken cancellationToken)
    {
        Language? language = await db.Languages.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == id && l.IsDeleted, cancellationToken);
        if (language is null)
            return Result.Failure(TrashErrors.NotFound);

        if (language.IsDefault && await db.Languages.AnyAsync(l => l.IsDefault, cancellationToken))
            language.IsDefault = false;

        language.IsDeleted = false;
        language.IsActive = true;
        language.DeleteDate = null;
        language.DeleteUserId = null;
        return Result.Success();
    }

    private async Task<Result> RestoreSiteSettingAsync(int id, CancellationToken cancellationToken)
    {
        SiteSetting? setting = await db.SiteSettings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.IsDeleted, cancellationToken);
        if (setting is null)
            return Result.Failure(TrashErrors.NotFound);

        bool keyTaken = await db.SiteSettings
            .AnyAsync(s => s.Id != setting.Id && s.Key == setting.Key && s.Group == setting.Group, cancellationToken);
        if (keyTaken)
            return Result.Failure(SiteSettingErrors.KeyAlreadyExists);

        setting.IsDeleted = false;
        setting.IsActive = true;
        setting.DeleteDate = null;
        setting.DeleteUserId = null;
        return Result.Success();
    }

    private static async Task<Result> RestoreSimpleAsync<T>(DbSet<T> set, int id, CancellationToken cancellationToken)
        where T : BaseEntity
    {
        T? entity = await set.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id && e.IsDeleted, cancellationToken);
        if (entity is null)
            return Result.Failure(TrashErrors.NotFound);

        entity.IsDeleted = false;
        entity.IsActive = true;
        entity.DeleteDate = null;
        entity.DeleteUserId = null;
        return Result.Success();
    }
}
