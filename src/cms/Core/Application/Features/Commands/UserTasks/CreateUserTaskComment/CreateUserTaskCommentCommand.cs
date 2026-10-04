using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.UserTasks.CreateUserTaskComment;

public sealed record CreateUserTaskCommentCommand(int UserTaskId, string Comment) : ICommand;
