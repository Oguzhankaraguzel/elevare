using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.UserTasks.DeleteUserTaskComment;

public sealed record DeleteUserTaskCommentCommand(int CommentId) : ICommand;
