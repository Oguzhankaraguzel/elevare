using Application.Abstraction.Services;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Cache.GetSiteCacheStatus;

internal sealed class GetSiteCacheStatusQueryHandler(ICacheClearService cacheClearService)
    : IQueryHandler<GetSiteCacheStatusQuery, SiteCacheStatus>
{
    public Task<Result<SiteCacheStatus>> Handle(GetSiteCacheStatusQuery request, CancellationToken cancellationToken) =>
        cacheClearService.GetStatusAsync(cancellationToken);
}
