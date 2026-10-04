using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.PageTemplates.DeletePageTemplate;

public sealed record DeletePageTemplateCommand(int Id) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.TemplatesDelete;
}
