using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Languages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Languages.DeleteLanguage;

internal sealed class DeleteLanguageCommandHandler(ICmsApplicationDbContext Db) : ICommandHandler<DeleteLanguageCommand>
{
    public async Task<Result> Handle(DeleteLanguageCommand request, CancellationToken cancellationToken)
    {
        Language? lang = await Db.Languages.FirstOrDefaultAsync(l => l.Id == request.Id && !l.IsDeleted, cancellationToken);

        if (lang is null)
            return Result.Failure(LanguageErrors.NotFound);

        if (lang.IsDefault)
            return Result.Failure(LanguageErrors.CannotDeleteDefaultLanguage);

        // Its pages would otherwise stay behind under a language that no longer
        // exists: gone from the site and the page list, with nothing in the Trash to
        // bring back. Taking a language off the site is the "On the site" switch.
        if (await Db.PageInfos.AnyAsync(p => p.LanguageId == lang.Id, cancellationToken))
            return Result.Failure(LanguageErrors.HasPages);

        Db.Languages.Remove(lang);

        return Result.Success();
    }
}
