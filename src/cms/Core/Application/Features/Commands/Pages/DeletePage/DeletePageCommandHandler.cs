using Application.Abstraction.Data;
using Domain.Entities.PageInfos;
using Application.Features.Commands.Trash;
using Domain.Entities.Redirects;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Pages.DeletePage;

internal sealed class DeletePageCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<DeletePageCommand>
{
    public async Task<Result> Handle(DeletePageCommand request, CancellationToken cancellationToken)
    {
        PageInfo? page = await db.PageInfos
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (page is null)
            return Result.Failure(PageInfoErrors.NotFound);

        bool hasChildren = await db.PageInfos
            .AnyAsync(p => p.ParentPageId == page.Id, cancellationToken);
        if (hasChildren)
            return Result.Failure(PageInfoErrors.CannotDeleteWithChildren);

        bool wasPublished = page.PageStatus == PageStatus.Published;
        if (wasPublished && page.Slug == "home")
            return Result.Failure(PageInfoErrors.CannotUnpublishHomePage);

        string fullSlug = page.FullSlug;

        // One tombstone per address. Deleting and recreating the same page is an
        // ordinary editing loop, and without this each pass leaves another
        // soft-deleted row on the same slug+language — none of which can ever be
        // restored once a live page holds the address again. The Trash is meant to
        // answer "undo the last delete", not to accumulate every attempt.
        // Purge is best-effort: a tombstone protected by a safety check (form
        // submissions, sub-pages) is left where it is rather than blocking this delete.
        List<int> olderTombstones = await db.PageInfos
            .IgnoreQueryFilters()
            .Where(p => p.IsDeleted && p.Id != page.Id
                        && p.Slug == page.Slug && p.LanguageId == page.LanguageId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (int tombstoneId in olderTombstones)
            await TrashPurge.PurgePageAsync(db, tombstoneId, cancellationToken);

        // Soft delete (the audit interceptor turns Remove into IsDeleted = true).
        db.PageInfos.Remove(page);

        // Best-effort: a redirect couldn't be created shouldn't block the delete itself.
        if (wasPublished)
            await RedirectResolution.UpsertForPageRemovedAsync(
                db, page.Id, fullSlug, request.RedirectTargetUrl, RedirectReason.PageDeleted, cancellationToken);

        return Result.Success();
    }
}
