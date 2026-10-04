using Application.Abstraction.Data;
using Application.Features.Trash;
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
using Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Strings;

namespace Application.Features.Queries.Trash.GetTrashedItems;

internal sealed class GetTrashedItemsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetTrashedItemsQuery, List<TrashItemResponse>>
{
    public async Task<Result<List<TrashItemResponse>>> Handle(GetTrashedItemsQuery request, CancellationToken cancellationToken)
    {
        List<TrashItemResponse> items = [];

        List<PageInfo> pages = await db.PageInfos.IgnoreQueryFilters()
            .Where(p => p.IsDeleted)
            .Include(p => p.DeleteUser)
            .ToListAsync(cancellationToken);
        foreach (PageInfo page in pages)
        {
            TrashConflictKind conflict = TrashConflictKind.None;
            bool slugTaken = await db.PageInfos
                .AnyAsync(p => p.Id != page.Id && p.Slug == page.Slug && p.LanguageId == page.LanguageId, cancellationToken);
            if (slugTaken)
            {
                conflict = TrashConflictKind.SlugTaken;
            }
            else if (await db.Redirects.AnyAsync(r => r.OldPath == page.FullSlug, cancellationToken))
            {
                conflict = TrashConflictKind.RedirectExists;
            }

            // Mirrors TrashPurge.CanPurgePageAsync. Kept as its own check rather than
            // calling it so the list stays a single pass of reads per page.
            TrashPurgeBlock purgeBlock = TrashPurgeBlock.None;
            if (await db.FormSubmissions.IgnoreQueryFilters().AnyAsync(f => f.PageInfoId == page.Id, cancellationToken))
                purgeBlock = TrashPurgeBlock.HasFormSubmissions;
            else if (await db.PageInfos.IgnoreQueryFilters().AnyAsync(p => p.ParentPageId == page.Id, cancellationToken))
                purgeBlock = TrashPurgeBlock.HasChildren;

            items.Add(new TrashItemResponse(
                TrashEntityType.PageInfo, page.Id,
                page.SeoMeta.Title.HasValue() ? page.SeoMeta.Title : page.Slug,
                page.DeleteDate ?? page.UpdateDate ?? page.CreateDate,
                DeletedByName(page.DeleteUser), conflict, purgeBlock));
        }

        List<PageTemplate> templates = await db.PageTemplates.IgnoreQueryFilters()
            .Where(t => t.IsDeleted).Include(t => t.DeleteUser).ToListAsync(cancellationToken);
        items.AddRange(templates.Select(t => new TrashItemResponse(
            TrashEntityType.PageTemplate, t.Id, t.Name,
            t.DeleteDate ?? t.UpdateDate ?? t.CreateDate, DeletedByName(t.DeleteUser))));

        List<MediaFile> media = await db.MediaFiles.IgnoreQueryFilters()
            .Where(m => m.IsDeleted).Include(m => m.DeleteUser).ToListAsync(cancellationToken);
        items.AddRange(media.Select(m => new TrashItemResponse(
            TrashEntityType.MediaFile, m.Id, m.Title.HasValue() ? m.Title! : m.FileName,
            m.DeleteDate ?? m.UpdateDate ?? m.CreateDate, DeletedByName(m.DeleteUser))));

        List<Language> languages = await db.Languages.IgnoreQueryFilters()
            .Where(l => l.IsDeleted).Include(l => l.DeleteUser).ToListAsync(cancellationToken);
        items.AddRange(languages.Select(l => new TrashItemResponse(
            TrashEntityType.Language, l.Id, l.NameInNative,
            l.DeleteDate ?? l.UpdateDate ?? l.CreateDate, DeletedByName(l.DeleteUser))));

        List<SiteSetting> settings = await db.SiteSettings.IgnoreQueryFilters()
            .Where(s => s.IsDeleted).Include(s => s.DeleteUser).ToListAsync(cancellationToken);
        items.AddRange(settings.Select(s => new TrashItemResponse(
            TrashEntityType.SiteSetting, s.Id, s.DisplayName,
            s.DeleteDate ?? s.UpdateDate ?? s.CreateDate, DeletedByName(s.DeleteUser))));

        List<UserTask> tasks = await db.UserTasks.IgnoreQueryFilters()
            .Where(t => t.IsDeleted).Include(t => t.DeleteUser).ToListAsync(cancellationToken);
        items.AddRange(tasks.Select(t => new TrashItemResponse(
            TrashEntityType.UserTask, t.Id, t.Title,
            t.DeleteDate ?? t.UpdateDate ?? t.CreateDate, DeletedByName(t.DeleteUser))));

        List<UserNote> notes = await db.UserNotes.IgnoreQueryFilters()
            .Where(n => n.IsDeleted).Include(n => n.DeleteUser).ToListAsync(cancellationToken);
        items.AddRange(notes.Select(n => new TrashItemResponse(
            TrashEntityType.UserNote, n.Id, n.Title,
            n.DeleteDate ?? n.UpdateDate ?? n.CreateDate, DeletedByName(n.DeleteUser))));

        List<UserReminder> reminders = await db.UserReminders.IgnoreQueryFilters()
            .Where(r => r.IsDeleted).Include(r => r.DeleteUser).ToListAsync(cancellationToken);
        items.AddRange(reminders.Select(r => new TrashItemResponse(
            TrashEntityType.UserReminder, r.Id, r.Title,
            r.DeleteDate ?? r.UpdateDate ?? r.CreateDate, DeletedByName(r.DeleteUser))));

        List<ContentBulkEdit> bulkEdits = await db.ContentBulkEdits.IgnoreQueryFilters()
            .Where(e => e.IsDeleted).Include(e => e.DeleteUser).ToListAsync(cancellationToken);
        items.AddRange(bulkEdits.Select(e => new TrashItemResponse(
            TrashEntityType.ContentBulkEdit, e.Id, $"\"{e.SearchText}\" → \"{e.ReplaceText}\"",
            e.DeleteDate ?? e.UpdateDate ?? e.CreateDate, DeletedByName(e.DeleteUser))));

        List<SiteCodeSnippet> snippets = await db.SiteCodeSnippets.IgnoreQueryFilters()
            .Where(s => s.IsDeleted).Include(s => s.DeleteUser).ToListAsync(cancellationToken);
        items.AddRange(snippets.Select(s => new TrashItemResponse(
            TrashEntityType.SiteCodeSnippet, s.Id, s.Name,
            s.DeleteDate ?? s.UpdateDate ?? s.CreateDate, DeletedByName(s.DeleteUser))));

        return Result.Success(items.OrderByDescending(i => i.DeletedAt).ToList());
    }

    private static string? DeletedByName(AppUser? user)
    {
        if (user is null) return null;
        return user.FullName.HasValue() ? user.FullName : user.UserName;
    }
}
