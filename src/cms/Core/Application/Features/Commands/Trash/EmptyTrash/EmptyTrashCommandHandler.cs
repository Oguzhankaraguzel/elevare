using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Trash.EmptyTrash;

internal sealed class EmptyTrashCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<EmptyTrashCommand, EmptyTrashResult>
{
    public async Task<Result<EmptyTrashResult>> Handle(EmptyTrashCommand request, CancellationToken cancellationToken)
    {
        int purged = 0;
        int skipped = 0;

        // Pages first and one at a time: each has to clear its own safety checks, and
        // the ones that cannot are counted rather than aborting the run.
        List<int> pageIds = await db.PageInfos
            .IgnoreQueryFilters()
            .Where(p => p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (int pageId in pageIds)
        {
            Result result = await TrashPurge.PurgePageAsync(db, pageId, cancellationToken);
            if (result.IsSuccess) purged++; else skipped++;
        }

        purged += await TrashPurge.PurgeAllAsync(db.PageTemplates, db, cancellationToken);
        purged += await TrashPurge.PurgeAllAsync(db.MediaFiles, db, cancellationToken);
        purged += await TrashPurge.PurgeAllAsync(db.SiteSettings, db, cancellationToken);
        purged += await TrashPurge.PurgeAllAsync(db.UserTasks, db, cancellationToken);
        purged += await TrashPurge.PurgeAllAsync(db.UserNotes, db, cancellationToken);
        purged += await TrashPurge.PurgeAllAsync(db.UserReminders, db, cancellationToken);
        purged += await TrashPurge.PurgeAllAsync(db.ContentBulkEdits, db, cancellationToken);
        purged += await TrashPurge.PurgeAllAsync(db.SiteCodeSnippets, db, cancellationToken);

        // Languages are deliberately left out. A deleted language still owns every
        // page written in it, so purging one would either fail on those references
        // or take the content with it — that is a decision for the Languages screen,
        // not a side effect of tidying up.

        return Result.Success(new EmptyTrashResult(purged, skipped));
    }

}
