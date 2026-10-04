using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.StructuredData.BuildDraft;

/// <summary>
/// Produces a starting structured-data graph for a page, from data the CMS
/// already holds — nothing is invented and nothing is asked of the editor.
/// <para>
/// Returns the JSON rather than saving it: the editor reviews the draft in the
/// builder and saves the page as usual. A command that wrote straight to the page
/// would edit published output on a button press.
/// </para>
/// </summary>
/// <param name="PageId">Page to build the draft for.</param>
public sealed record BuildStructuredDataDraftCommand(int PageId) : ICommand<string>, IRequirePermission
{
    // Same gate as RefreshStructuredData: this reads a page's content to draft its
    // schema, invoked only from the page editor's structured-data builder.
    public static string RequiredPermission => PermissionKeys.PagesEdit;
}
