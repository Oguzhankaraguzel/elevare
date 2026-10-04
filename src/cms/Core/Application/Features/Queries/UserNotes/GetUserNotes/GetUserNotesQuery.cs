using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.UserNotes.GetUserNotes;

public sealed record GetUserNotesQuery(bool? IsArchived = null, bool? IsPinned = null)
    : IQuery<PagedResult<UserNoteResponse>>;
