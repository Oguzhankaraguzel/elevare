using Application.Abstraction.Data;
using Domain.Entities.SiteCodeSnippets;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.SiteCodeSnippets.MoveSiteCodeSnippet;

internal sealed class MoveSiteCodeSnippetCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<MoveSiteCodeSnippetCommand>
{
    public async Task<Result> Handle(MoveSiteCodeSnippetCommand request, CancellationToken cancellationToken)
    {
        SiteCodeSnippet? snippet = await db.SiteCodeSnippets
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (snippet is null)
            return Result.Failure(SiteCodeSnippetErrors.NotFound);

        // Same ordering the public page uses, so "the one above" on screen is the
        // one that actually renders first.
        List<SiteCodeSnippet> siblings = await db.SiteCodeSnippets
            .Where(s => s.Placement == snippet.Placement)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        int index = siblings.FindIndex(s => s.Id == snippet.Id);
        int targetIndex = request.Up ? index - 1 : index + 1;

        // Already at the end it is being pushed towards — nothing to do, and not an
        // error worth interrupting anyone over.
        if (targetIndex < 0 || targetIndex >= siblings.Count)
            return Result.Success();

        SiteCodeSnippet neighbour = siblings[targetIndex];
        (snippet.SortOrder, neighbour.SortOrder) = (neighbour.SortOrder, snippet.SortOrder);

        // Seeded rows can share a SortOrder, in which case swapping the values alone
        // changes nothing; fall back to renumbering the whole placement.
        if (snippet.SortOrder == neighbour.SortOrder)
        {
            (siblings[index], siblings[targetIndex]) = (siblings[targetIndex], siblings[index]);
            for (int i = 0; i < siblings.Count; i++)
                siblings[i].SortOrder = i * 10;
        }

        return Result.Success();
    }
}
