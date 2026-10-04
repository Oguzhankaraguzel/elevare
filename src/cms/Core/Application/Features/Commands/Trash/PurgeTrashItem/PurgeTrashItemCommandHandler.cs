using Application.Abstraction.Data;
using Application.Features.Trash;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Trash.PurgeTrashItem;

internal sealed class PurgeTrashItemCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<PurgeTrashItemCommand>
{
    public Task<Result> Handle(PurgeTrashItemCommand request, CancellationToken cancellationToken) => request.EntityType switch
    {
        TrashEntityType.PageInfo => TrashPurge.PurgePageAsync(db, request.Id, cancellationToken),
        TrashEntityType.PageTemplate => TrashPurge.PurgeSimpleAsync(db.PageTemplates, db, request.Id, cancellationToken),
        TrashEntityType.MediaFile => TrashPurge.PurgeSimpleAsync(db.MediaFiles, db, request.Id, cancellationToken),
        TrashEntityType.Language => TrashPurge.PurgeSimpleAsync(db.Languages, db, request.Id, cancellationToken),
        TrashEntityType.SiteSetting => TrashPurge.PurgeSimpleAsync(db.SiteSettings, db, request.Id, cancellationToken),
        TrashEntityType.UserTask => TrashPurge.PurgeSimpleAsync(db.UserTasks, db, request.Id, cancellationToken),
        TrashEntityType.UserNote => TrashPurge.PurgeSimpleAsync(db.UserNotes, db, request.Id, cancellationToken),
        TrashEntityType.UserReminder => TrashPurge.PurgeSimpleAsync(db.UserReminders, db, request.Id, cancellationToken),
        TrashEntityType.ContentBulkEdit => TrashPurge.PurgeSimpleAsync(db.ContentBulkEdits, db, request.Id, cancellationToken),
        TrashEntityType.SiteCodeSnippet => TrashPurge.PurgeSimpleAsync(db.SiteCodeSnippets, db, request.Id, cancellationToken),
        _ => Task.FromResult(Result.Failure(TrashErrors.UnknownType)),
    };
}
