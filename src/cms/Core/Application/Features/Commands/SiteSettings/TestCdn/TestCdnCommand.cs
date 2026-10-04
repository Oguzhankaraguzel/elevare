using Application.Abstraction.Security;
using Application.Abstraction.Services;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.SiteSettings.TestCdn;

/// <summary>
/// Pulls a real uploaded file through <paramref name="CdnBaseUrl"/> and reports what
/// came back. A command rather than a query because it reaches out to a third party:
/// it costs a request to someone else's edge and must only ever run when a person
/// asks for it, never as part of rendering a screen.
/// </summary>
// string, not Uri — same reason as ICdnProbe.CheckAsync: this carries unvalidated
// text straight from the settings form, and "not a URL at all" is a result the
// operator is meant to see rather than a binding error.
#pragma warning disable CA1054
public sealed record TestCdnCommand(string CdnBaseUrl) : ICommand<CdnProbeResult>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.SiteSettingsManage;
}
#pragma warning restore CA1054
