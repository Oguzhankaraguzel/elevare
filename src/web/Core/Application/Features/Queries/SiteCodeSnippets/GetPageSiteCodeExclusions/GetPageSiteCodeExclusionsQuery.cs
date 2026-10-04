using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.SiteCodeSnippets.GetPageSiteCodeExclusions;

/// <summary>The ids of the site codes switched off on one page — see <see cref="Application.Services.SiteCodeExclusions"/>.</summary>
public sealed record GetPageSiteCodeExclusionsQuery(int PageId) : IQuery<HashSet<int>>;
