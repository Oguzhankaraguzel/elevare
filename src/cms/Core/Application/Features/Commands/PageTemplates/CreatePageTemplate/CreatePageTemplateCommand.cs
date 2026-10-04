using Domain.Entities.PageTemplates;
using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.PageTemplates.CreatePageTemplate;

/// <summary>Creates a new (empty) reusable template. Returns the new id so the editor can open it.</summary>
public sealed record CreatePageTemplateCommand(
    string Name,
    PageTemplateType Type,
    bool IsLinked,
    int? LanguageId = null) : ICommand<int>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.TemplatesCreate;
}
