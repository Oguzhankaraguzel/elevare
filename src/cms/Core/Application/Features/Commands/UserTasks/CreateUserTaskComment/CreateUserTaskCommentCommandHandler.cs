using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserTasks;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.UserTasks.CreateUserTaskComment;

internal sealed record CreateUserTaskCommentCommandHandler(
    ICmsApplicationDbContext Db,
    IUserContext UserContext) : ICommandHandler<CreateUserTaskCommentCommand>
{
    public async Task<Result> Handle(CreateUserTaskCommentCommand request, CancellationToken cancellationToken)
    {
        bool taskExists = await Db.UserTasks
            .AnyAsync(t => t.Id == request.UserTaskId && !t.IsDeleted, cancellationToken);

        if (!taskExists)
            return Result.Failure(UserTaskErrors.NotFound);

        var comment = new UserTaskComment
        {
            UserTaskId = request.UserTaskId,
            UserId = UserContext.UserId,
            Comment = request.Comment
        };

        Db.UserTaskComments.Add(comment);

        return Result.Success();
    }
}
