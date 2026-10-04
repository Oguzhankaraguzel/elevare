using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.UserNotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Announcements.MarkAnnouncementsRead;

internal sealed class MarkAnnouncementsReadCommandHandler(ICmsApplicationDbContext db, IUserContext userContext)
    : ICommandHandler<MarkAnnouncementsReadCommand>
{
    public async Task<Result> Handle(MarkAnnouncementsReadCommand request, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        List<int> unreadIds = await db.UserNotes
            .Where(n => n.Visibility == NoteVisibility.Everyone
                        && n.IsActive
                        && !n.IsArchived
                        && !db.AnnouncementReads.Any(r => r.UserNoteId == n.Id && r.UserId == userId))
            .Select(n => n.Id)
            .ToListAsync(cancellationToken);

        foreach (int noteId in unreadIds)
        {
            db.AnnouncementReads.Add(new AnnouncementRead
            {
                UserNoteId = noteId,
                UserId = userId,
                ReadAtUtc = DateTime.UtcNow,
            });
        }

        return Result.Success();
    }
}
