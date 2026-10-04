using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserTasks;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.UserTasks.UpdateUserTaskStatus;

internal sealed record UpdateUserTaskStatusCommandHandler(ICmsApplicationDbContext Db)
    : ICommandHandler<UpdateUserTaskStatusCommand>
{
    public async Task<Result> Handle(UpdateUserTaskStatusCommand request, CancellationToken cancellationToken)
    {
        UserTask? task = await Db.UserTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);

        if (task is null)
            return Result.Failure(UserTaskErrors.NotFound);

        if (task.Status == UserTaskStatus.Completed && request.Status != UserTaskStatus.Completed)
            task.CompletedAt = null;

        if (request.Status == UserTaskStatus.Completed && task.Status != UserTaskStatus.Completed)
            task.CompletedAt = DateTime.UtcNow;

        task.Status = request.Status;

        return Result.Success();
    }
}
