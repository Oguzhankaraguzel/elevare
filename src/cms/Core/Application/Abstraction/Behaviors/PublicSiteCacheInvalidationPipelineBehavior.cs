using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Application.Features.Commands.Cache.ClearSiteCache;
using MediatR;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Abstraction.Behaviors;

/// <summary>
/// After every command, asks the public site to drop the rendered pages the
/// command's saves made stale.
/// </summary>
/// <remarks>
/// What is stale is read from what was actually saved (<see cref="PublicSiteChange"/>,
/// built by the database context), not from a list of "content" commands: a page's
/// HTML is built from pages, templates, media, site settings, site codes, languages,
/// redirects, tags and more, and a list of commands would go stale the first time
/// someone adds a feature and forgets it. A table the classifier does not know
/// clears the whole site, as every command did before; a note or a task clears
/// nothing; a page or a linked template clears the pages built from it. A command
/// that succeeded without a save the context could see clears everything — it may
/// have changed the site some other way. Registered outside
/// <see cref="SaveChangesPipelineBehavior{TRequest,TResponse}"/> so it runs only
/// once the changes are actually committed.
/// </remarks>
internal sealed class PublicSiteCacheInvalidationPipelineBehavior<TRequest, TResponse>(
    IPublicSiteCacheInvalidator invalidator,
    ICmsApplicationDbContext db)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        TResponse response = await next();

        // ClearSiteCacheCommand already is that call.
        if (request is not IBaseCommand || request is ClearSiteCacheCommand)
            return response;

        PublicSiteChange change = db.TakePublicSiteChange();
        if (change.Saves == 0)
        {
            if (response is Result { IsSuccess: true })
                invalidator.RequestInvalidation(PublicSiteChange.Everything());
        }
        // Also after a failure that saved first: what was saved is live either way.
        else if (!change.IsEmpty)
        {
            invalidator.RequestInvalidation(change);
        }

        return response;
    }
}
