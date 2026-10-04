using Domain.Entities.PageTemplates;
using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.PageTemplates.UpdatePageTemplate;

public sealed record UpdatePageTemplateCommand(
    int Id,
    string Name,
    PageTemplateType Type,
    bool IsLinked,
    string? GjsHtml,
    string? GjsCss,
    string? GjsData,
    int? LanguageId = null,
    // See UpdatePageCommand: the template as the editor loaded it, and the author's
    // choice to save over a change made since.
    string? ExpectedFingerprint = null,
    bool Overwrite = false) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.TemplatesEdit;
}
