using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserTasks;
using SharedKernel.Concrete;
namespace Application.Features.Commands.UserTasks.CreateUserTask;

internal sealed record CreateUserTaskCommandHandler(
    ICmsApplicationDbContext Db,
    IUserContext UserContext) : ICommandHandler<CreateUserTaskCommand>
{
    // Not async: the row is only staged here — SaveChangesPipelineBehavior commits
    // it. Marking this async without an await compiles under the .NET 10 preview
    // SDK but is CS1998 on the .NET 9 SDK the project actually targets.
    public Task<Result> Handle(CreateUserTaskCommand request, CancellationToken cancellationToken)
    {
        var task = new UserTask
        {
            Title = request.Title,
            Description = request.Description,
            AssignedToUserId = request.AssignedToUserId,
            AssignedByUserId = UserContext.UserId,
            Priority = request.Priority,
            Status = UserTaskStatus.New,
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            ParentTaskId = request.ParentTaskId
        };

        Db.UserTasks.Add(task);

        return Task.FromResult(Result.Success());
    }
}
