using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.UserNotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Announcements.GetAnnouncements;

internal sealed class GetAnnouncementsQueryHandler(ICmsApplicationDbContext db, IUserContext userContext)
    : IQueryHandler<GetAnnouncementsQuery, AnnouncementListResponse>
{
    public async Task<Result<AnnouncementListResponse>> Handle(
        GetAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        IQueryable<UserNote> announcements = db.UserNotes
            .AsNoTracking()
            .Where(n => n.Visibility == NoteVisibility.Everyone && n.IsActive && !n.IsArchived);

        // Read state is a left join, not a subquery per row: one query answers both
        // "which of these have I seen" and, below, "how many are still waiting".
        var rows = await announcements
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreateDate)
            .Take(request.Take)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Content,
                n.Color,
                n.IsPinned,
                n.CreateDate,
                First = n.CreateUser.FirstName,
                Last = n.CreateUser.LastName,
                n.CreateUser.UserName,
                IsRead = db.AnnouncementReads.Any(r => r.UserNoteId == n.Id && r.UserId == userId),
            })
            .ToListAsync(cancellationToken);

        int unreadCount = await announcements
            .CountAsync(n => !db.AnnouncementReads.Any(r => r.UserNoteId == n.Id && r.UserId == userId), cancellationToken);

        List<AnnouncementResponse> items = [.. rows.Select(n => new AnnouncementResponse(
            n.Id,
            n.Title,
            n.Content,
            n.Color,
            n.IsPinned,
            DisplayName(n.First, n.Last, n.UserName),
            n.CreateDate,
            n.IsRead))];

        return Result.Success(new AnnouncementListResponse(unreadCount, items));
    }

    private static string DisplayName(string? first, string? last, string? userName)
    {
        string full = $"{first} {last}".Trim();
        return full.Length > 0 ? full : userName ?? "";
    }
}
