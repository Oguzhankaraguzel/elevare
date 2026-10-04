using Application.Abstraction.Data;
using Application.Abstraction.Services;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Languages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Languages.UpdateLanguage;

internal sealed record UpdateLanguageCommandHandler(ICmsApplicationDbContext Db, ISitemapRegenerator Sitemaps)
    : ICommandHandler<UpdateLanguageCommand, LanguageSaveResult>
{
    public async Task<Result<LanguageSaveResult>> Handle(UpdateLanguageCommand request, CancellationToken cancellationToken)
    {
        Language? lang = await Db.Languages.FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

        if (lang is null)
            return Result.Failure<LanguageSaveResult>(LanguageErrors.NotFound);

        bool becomingDefault = request.IsDefault && !lang.IsDefault;

        if (becomingDefault)
        {
            List<Language> previousDefaults = await Db.Languages
                .Where(x => x.IsDefault && x.Id != request.Id)
                .ToListAsync(cancellationToken);
            foreach (Language previous in previousDefaults)
                previous.IsDefault = false;

            // The unique filtered index backing "only one default language at a
            // time" is enforced per statement, not once at commit — if this clear
            // and the new default's own IsDefault=true below reached the database
            // in the same batch, SQL Server is free to run them in either order,
            // and "new one first" collides with the still-true old row. Saving here
            // forces the clear to land first no matter how EF would have batched it.
            await Db.SaveChangesAsync(cancellationToken);
        }


        lang.NameInNative = request.NameInNative;
        lang.NameInEnglish = request.NameInEnglish;
        lang.TwoLetterCode = request.TwoLetterCode;
        lang.IsDefault = request.IsDefault;
        lang.FlagIconFileId = request.FlagIconFileId;
        lang.IsRtl = request.IsRtl;
        lang.IsPublished = request.IsPublished;
        lang.DisplayOrder = request.DisplayOrder;
        lang.IsActive = request.IsActive;

        // Every page's FullSlug omits the language prefix only for whichever language
        // is default — see PageHierarchy.RecomputeAllSlugsForDefaultLanguageChangeAsync.
        // Skipping this on a default switch 404s the whole site.
        if (becomingDefault)
            return Result.Success(await DefaultLanguageSwitch.ApplyAsync(Db, Sitemaps, request.TwoLetterCode, cancellationToken));

        return Result.Success(LanguageSaveResult.Unchanged);
    }
}
