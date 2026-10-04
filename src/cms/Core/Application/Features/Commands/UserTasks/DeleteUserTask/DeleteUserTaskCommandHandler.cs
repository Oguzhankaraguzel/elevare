using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserTasks;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.UserTasks.DeleteUserTask;

internal sealed class DeleteUserTaskCommandHandler(ICmsApplicationDbContext Db) : ICommandHandler<DeleteUserTaskCommand>
{
    public async Task<Result> Handle(DeleteUserTaskCommand request, CancellationToken cancellationToken)
    {
        UserTask? task = await Db.UserTasks.FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);

        if (task is null)
            return Result.Failure(UserTaskErrors.NotFound);

        Db.UserTasks.Remove(task);

        return Result.Success();
    }
}
