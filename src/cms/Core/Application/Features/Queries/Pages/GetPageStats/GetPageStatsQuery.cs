using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetPageStats;

public sealed record GetPageStatsQuery(int PageId) : IQuery<PageStatsResponse>;
