using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.FormReplyTemplates.DeleteFormReplyTemplate;

public sealed record DeleteFormReplyTemplateCommand(int Id) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.FormsManageActions;
}
