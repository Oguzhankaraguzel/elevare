using Application.Abstraction.Services;
using Application.Features.Commands.Cache.ClearSiteCache;
using MediatR;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Abstraction.Behaviors;

/// <summary>
/// After every successful command, asks the public site to drop its rendered pages.
/// </summary>
/// <remarks>
/// Deliberately every command rather than a list of "content" ones: a page's HTML
/// is built from pages, templates, media, site settings, site codes, languages,
/// redirects, tags and more, and a list would go stale the first time someone adds
/// a feature and forgets it. A command that changes nothing public (a note, a task)
/// costs one extra cache refill, which is cheap; a missed one would leave the site
/// showing old content for up to an hour. Registered outside
/// <see cref="SaveChangesPipelineBehavior{TRequest,TResponse}"/> so it runs only
/// once the changes are actually committed.
/// </remarks>
internal sealed class PublicSiteCacheInvalidationPipelineBehavior<TRequest, TResponse>(
    IPublicSiteCacheInvalidator invalidator)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        TResponse response = await next();

        // ClearSiteCacheCommand already is that call.
        if (request is IBaseCommand and not ClearSiteCacheCommand && response is Result { IsSuccess: true })
            invalidator.RequestInvalidation();

        return response;
    }
}
