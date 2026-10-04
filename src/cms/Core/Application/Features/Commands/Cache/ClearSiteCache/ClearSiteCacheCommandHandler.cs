using Application.Abstraction.Services;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Cache.ClearSiteCache;

internal sealed class ClearSiteCacheCommandHandler(ICacheClearService cacheClearService)
    : ICommandHandler<ClearSiteCacheCommand>
{
    public Task<Result> Handle(ClearSiteCacheCommand request, CancellationToken cancellationToken) =>
        cacheClearService.ClearAsync(cancellationToken);
}
