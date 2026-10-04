using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.FormReplyTemplates.GetFormReplyTemplates;

/// <param name="ActiveOnly">
/// True when picking a template to answer with; false on the management page, which
/// has to be able to see and re-enable a retired one.
/// </param>
public sealed record GetFormReplyTemplatesQuery(bool ActiveOnly = false)
    : IQuery<List<FormReplyTemplateResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.FormsViewSubmissions;
}
