using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.StructuredData.RefreshNodes;

/// <summary>
/// Re-derives the site-level and hierarchy nodes across every page that already has
/// structured data, after Site Settings or the page tree changed.
/// </summary>
public sealed record RefreshStructuredDataCommand : ICommand<RefreshStructuredDataResult>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.PagesEdit;
}
