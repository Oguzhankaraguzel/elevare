using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Redirects.CreateRedirect;

/// <summary>
/// Adds a redirect rule by hand — for URLs that existed before this CMS did, or for
/// campaign short links, neither of which any page rename would have produced.
/// </summary>
/// <param name="NewPath">
/// Null or empty means 410 Gone: the page is deliberately retired with no successor.
/// </param>
/// <param name="IsTemporary">
/// Answer 302 instead of 301. Permanent is the default because it is what a moved
/// page needs; temporary suits a campaign link or a page that is coming back.
/// </param>
// NewPath is string rather than Uri on purpose: it is either a site-relative path
// or an absolute external URL, and Uri cannot represent the first.
#pragma warning disable CA1054
public sealed record CreateRedirectCommand(string OldPath, string? NewPath, bool IsTemporary = false)
#pragma warning restore CA1054
    : ICommand<int>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.RedirectsManage;
}
