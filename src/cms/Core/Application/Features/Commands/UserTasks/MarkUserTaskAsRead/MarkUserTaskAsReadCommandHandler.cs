using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserTasks;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.UserTasks.MarkUserTaskAsRead;

internal sealed record MarkUserTaskAsReadCommandHandler(ICmsApplicationDbContext Db)
    : ICommandHandler<MarkUserTaskAsReadCommand>
{
    public async Task<Result> Handle(MarkUserTaskAsReadCommand request, CancellationToken cancellationToken)
    {
        UserTask? task = await Db.UserTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);

        if (task is null)
            return Result.Failure(UserTaskErrors.NotFound);

        if (task.IsRead)
            return Result.Success();

        task.IsRead = true;

        return Result.Success();
    }
}
