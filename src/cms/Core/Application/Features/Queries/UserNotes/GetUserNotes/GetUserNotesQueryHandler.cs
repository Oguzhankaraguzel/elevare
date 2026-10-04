using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserNotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Queries.UserNotes.GetUserNotes;

internal sealed record GetUserNotesQueryHandler(
    ICmsApplicationDbContext Db,
    IUserContext UserContext)
    : IQueryHandler<GetUserNotesQuery, PagedResult<UserNoteResponse>>
{
    public async Task<Result<PagedResult<UserNoteResponse>>> Handle(
        GetUserNotesQuery request,
        CancellationToken cancellationToken)
    {
        Guid currentUserId = UserContext.UserId;
        bool isAdmin = UserContext.IsAdminOrAbove;

        IQueryable<UserNote> query = Db.UserNotes
            .AsNoTracking()
            .Where(n => !n.IsDeleted && (
                n.UserId == currentUserId ||
                n.Visibility == NoteVisibility.Everyone ||
                isAdmin && n.Visibility == NoteVisibility.AdminOnly
            ));

        if (request.IsArchived.HasValue)
            query = query.Where(n => n.IsArchived == request.IsArchived.Value);

        if (request.IsPinned.HasValue)
            query = query.Where(n => n.IsPinned == request.IsPinned.Value);

        var raw = await query
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreateDate)
            .Select(n => new
            {
                n.Id, n.Title, n.Content, n.Type, n.Visibility,
                n.Color, n.IsPinned, n.IsArchived, n.UserId, n.IsActive, n.CreateDate,
                CreateFirst = n.CreateUser.FirstName,
                CreateLast  = n.CreateUser.LastName,
                CreateUser  = n.CreateUser.UserName,
                n.UpdateDate,
                UpdateFirst = n.UpdateUser != null ? n.UpdateUser.FirstName : null,
                UpdateLast  = n.UpdateUser != null ? n.UpdateUser.LastName  : null,
                UpdateUser  = n.UpdateUser != null ? n.UpdateUser.UserName  : null,
            })
            .ToListAsync(cancellationToken);

        var items = raw.Select(n => new UserNoteResponse(
            n.Id, n.Title, n.Content, n.Type, n.Visibility,
            n.Color, n.IsPinned, n.IsArchived, n.UserId, n.IsActive, n.CreateDate,
            ResolveDisplayName(n.CreateFirst, n.CreateLast, n.CreateUser),
            n.UpdateDate,
            n.UpdateFirst is null && n.UpdateLast is null && n.UpdateUser is null
                ? null
                : ResolveDisplayName(n.UpdateFirst, n.UpdateLast, n.UpdateUser)
        )).ToList();

        return PagedResult<UserNoteResponse>.Create(items, items.Count, 1, items.Count);
    }

    private static string ResolveDisplayName(string? first, string? last, string? userName)
    {
        string fullName = $"{first} {last}".Trim();
        return fullName.Length > 0 ? fullName : userName ?? "";
    }
}
