using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.SiteCodeSnippets.GetSiteCodeSnippets;

/// <summary>
/// Every snippet, in the exact order the public page emits them.
/// <para>
/// Gated behind <c>CustomCode.Author</c> like the page it feeds: the rows hold
/// analytics keys and third-party account ids, which is not something to hand to
/// anyone who happens to be signed in.
/// </para>
/// </summary>
public sealed record GetSiteCodeSnippetsQuery : IQuery<List<SiteCodeSnippetResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.CustomCodeAuthor;
}
