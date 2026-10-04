using Application.Abstraction.Data;
using Application.Features.Trash;
using Domain.Entities.Abstractions;
using Domain.Entities.ContentBulkEdits;
using Domain.Entities.PageInfos;
using Domain.Entities.Redirects;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Trash;

/// <summary>
/// Permanent deletion of soft-deleted rows — the one exit the Trash previously did
/// not have. Shared by the manual purge, "empty trash", the retention job and the
/// delete flow's tombstone cleanup, so a row can never be destroyed through a path
/// that skipped the safety checks.
/// <para>
/// The checks are not cosmetic. <c>FormSubmissions.PageInfoId</c> and
/// <c>PageInfos.ParentPageId</c> are both NOT NULL and non-cascading, so a blind
/// delete either fails at the database or, worse, would have to take visitor data
/// with it.
/// </para>
/// <para>
/// Everything here goes through the change tracker and
/// <see cref="ICmsApplicationDbContext.RemovePermanently"/> rather than
/// <c>ExecuteDelete</c>: the set-based version cannot be exercised by the in-memory
/// provider the handler tests use, and a purge with no test is exactly the kind of
/// code that quietly destroys the wrong row.
/// </para>
/// </summary>
public static class TrashPurge
{
    /// <summary>
    /// Why a trashed page cannot be permanently deleted, or <c>Success</c> if it can.
    /// Exposed separately from <see cref="PurgePageAsync"/> so the Trash screen can
    /// grey the button out and say why, instead of offering a click that fails.
    /// </summary>
    public static async Task<Result> CanPurgePageAsync(
        ICmsApplicationDbContext db, int pageId, CancellationToken cancellationToken)
    {
        bool hasSubmissions = await db.FormSubmissions
            .IgnoreQueryFilters()
            .AnyAsync(f => f.PageInfoId == pageId, cancellationToken);
        if (hasSubmissions)
            return Result.Failure(TrashErrors.CannotPurgeHasFormSubmissions);

        bool hasChildren = await db.PageInfos
            .IgnoreQueryFilters()
            .AnyAsync(p => p.ParentPageId == pageId, cancellationToken);
        if (hasChildren)
            return Result.Failure(TrashErrors.CannotPurgeHasChildren);

        return Result.Success();
    }

    /// <summary>
    /// Deletes a soft-deleted page for good. PageContents and PageInfoTags cascade at
    /// the database; the two references that do not are handled deliberately:
    /// <list type="bullet">
    /// <item>a redirect that followed this page keeps working on its stored NewPath,
    /// so it is unbound rather than deleted — visitors holding the old URL still land
    /// somewhere;</item>
    /// <item>bulk-edit history rows are removed, because reverting an edit to a page
    /// that no longer exists has nothing to write to.</item>
    /// </list>
    /// </summary>
    public static async Task<Result> PurgePageAsync(
        ICmsApplicationDbContext db, int pageId, CancellationToken cancellationToken)
    {
        PageInfo? page = await db.PageInfos
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == pageId && p.IsDeleted, cancellationToken);
        if (page is null)
            return Result.Failure(TrashErrors.NotFound);

        Result allowed = await CanPurgePageAsync(db, pageId, cancellationToken);
        if (allowed.IsFailure)
            return allowed;

        List<Redirect> following = await db.Redirects
            .IgnoreQueryFilters()
            .Where(r => r.SourcePageId == pageId)
            .ToListAsync(cancellationToken);
        foreach (Redirect redirect in following)
            redirect.SourcePageId = null;

        List<ContentBulkEditItem> historyRows = await db.ContentBulkEditItems
            .IgnoreQueryFilters()
            .Where(i => i.PageInfoId == pageId)
            .ToListAsync(cancellationToken);
        foreach (ContentBulkEditItem item in historyRows)
            db.RemovePermanently(item);

        db.RemovePermanently(page);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Permanently deletes any soft-deleted row of a type with no dependants worth
    /// protecting.
    /// </summary>
    public static async Task<Result> PurgeSimpleAsync<T>(
        DbSet<T> set, ICmsApplicationDbContext db, int id, CancellationToken cancellationToken)
        where T : class, ISoftDeletable
    {
        T? entity = await set
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id && e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(TrashErrors.NotFound);

        db.RemovePermanently(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Permanently deletes every soft-deleted row of one type; returns how many went.</summary>
    public static async Task<int> PurgeAllAsync<T>(
        DbSet<T> set, ICmsApplicationDbContext db, CancellationToken cancellationToken)
        where T : class, ISoftDeletable
    {
        List<T> rows = await set.IgnoreQueryFilters().Where(e => e.IsDeleted).ToListAsync(cancellationToken);
        foreach (T row in rows)
            db.RemovePermanently(row);

        if (rows.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        return rows.Count;
    }
}
