using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserTasks;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.UserTasks.UpdateUserTask;

internal sealed record UpdateUserTaskCommandHandler(ICmsApplicationDbContext Db) : ICommandHandler<UpdateUserTaskCommand>
{
    public async Task<Result> Handle(UpdateUserTaskCommand request, CancellationToken cancellationToken)
    {
        UserTask? task = await Db.UserTasks.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (task is null)
            return Result.Failure(UserTaskErrors.NotFound);

        if (request.Status == UserTaskStatus.Completed && task.Status != UserTaskStatus.Completed)
            task.CompletedAt = DateTime.UtcNow;

        task.Title = request.Title;
        task.Description = request.Description;
        task.AssignedToUserId = request.AssignedToUserId;
        task.Status = request.Status;
        task.Priority = request.Priority;
        task.StartDate = request.StartDate;
        task.DueDate = request.DueDate;
        task.IsArchived = request.IsArchived;
        task.IsActive = request.IsActive;

        return Result.Success();
    }
}
