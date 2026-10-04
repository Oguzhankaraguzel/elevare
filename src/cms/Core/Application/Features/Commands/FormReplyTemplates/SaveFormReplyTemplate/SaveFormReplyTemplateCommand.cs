using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.FormReplyTemplates.SaveFormReplyTemplate;

/// <param name="Id">Null creates a new template; otherwise updates that one.</param>
public sealed record SaveFormReplyTemplateCommand(
    int? Id,
    string Name,
    string Subject,
    string Body,
    bool IsActive,
    int SortOrder) : ICommand<int>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.FormsManageActions;
}
