using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.SiteCodeSnippets.GetPublicSiteCodeSnippets;

/// <summary>
/// The enabled snippets grouped by placement, each list already in render order,
/// for the shared layout to write out.
/// </summary>
public sealed record GetPublicSiteCodeSnippetsQuery : IQuery<Dictionary<int, List<PublicSiteCodeEntry>>>;
