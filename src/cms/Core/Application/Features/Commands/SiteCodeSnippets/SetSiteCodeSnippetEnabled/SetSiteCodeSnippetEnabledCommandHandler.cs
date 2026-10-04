using Application.Abstraction.Data;
using Domain.Entities.SiteCodeSnippets;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.SiteCodeSnippets.SetSiteCodeSnippetEnabled;

internal sealed class SetSiteCodeSnippetEnabledCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<SetSiteCodeSnippetEnabledCommand>
{
    public async Task<Result> Handle(SetSiteCodeSnippetEnabledCommand request, CancellationToken cancellationToken)
    {
        SiteCodeSnippet? snippet = await db.SiteCodeSnippets
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (snippet is null)
            return Result.Failure(SiteCodeSnippetErrors.NotFound);

        snippet.IsEnabled = request.IsEnabled;

        return Result.Success();
    }
}
