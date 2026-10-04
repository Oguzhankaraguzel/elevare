using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserTasks;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.UserTasks.DeleteUserTaskComment;

internal sealed record DeleteUserTaskCommentCommandHandler(
    ICmsApplicationDbContext Db,
    IUserContext UserContext) : ICommandHandler<DeleteUserTaskCommentCommand>
{
    public async Task<Result> Handle(DeleteUserTaskCommentCommand request, CancellationToken cancellationToken)
    {
        UserTaskComment? comment = await Db.UserTaskComments
            .FirstOrDefaultAsync(c => c.Id == request.CommentId && !c.IsDeleted, cancellationToken);

        if (comment is null)
            return Result.Failure(UserTaskErrors.NotFound);

        if (comment.UserId != UserContext.UserId)
            return Result.Failure(UserTaskErrors.Unauthorized);

        Db.UserTaskComments.Remove(comment);

        return Result.Success();
    }
}
