using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Seo.GenerateLlmsTxt;

/// <summary>
/// Builds a starting llms.txt from the site's own published pages. Returns the draft
/// rather than saving it — the value of the file is the human judgement about which
/// pages matter, and a generator that wrote straight to the setting would encourage
/// nobody to apply any.
/// </summary>
public sealed record GenerateLlmsTxtQuery : IQuery<string>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.SeoManage;
}
