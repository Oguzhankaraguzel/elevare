using SharedKernel.Concrete;

namespace Domain.Entities.UserNotes;

public static class UserNoteErrors
{
    public static readonly Error Unauthorized = Error.Failure("UserNote.Unauthorized", "You are not authorized to access this note.");
}
