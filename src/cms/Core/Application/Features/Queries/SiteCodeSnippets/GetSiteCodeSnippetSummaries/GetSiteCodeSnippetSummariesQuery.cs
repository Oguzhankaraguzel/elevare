using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.SiteCodeSnippets.GetSiteCodeSnippetSummaries;

/// <summary>
/// Every snippet by name, placement and kind — no content — in the order the
/// public page emits them. What the page editor shows when an author decides
/// which site codes to switch off on one page.
/// <para>
/// Gated on <c>Pages.Edit</c>, not <c>CustomCode.Author</c> like
/// <see cref="GetSiteCodeSnippets.GetSiteCodeSnippetsQuery"/>: the names are not
/// the sensitive part (the analytics keys and account ids inside the content are),
/// and an editor who may not author code still has to be able to say "not the chat
/// widget on this landing page".
/// </para>
/// </summary>
public sealed record GetSiteCodeSnippetSummariesQuery : IQuery<List<SiteCodeSnippetSummaryResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.PagesEdit;
}
