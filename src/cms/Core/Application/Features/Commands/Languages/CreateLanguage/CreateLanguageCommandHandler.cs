using Application.Abstraction.Data;
using Application.Abstraction.Services;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Languages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Languages.CreateLanguage;

internal sealed record CreateLanguageCommandHandler(ICmsApplicationDbContext Db, ISitemapRegenerator Sitemaps)
    : ICommandHandler<CreateLanguageCommand, LanguageSaveResult>
{
    public async Task<Result<LanguageSaveResult>> Handle(CreateLanguageCommand request, CancellationToken cancellationToken)
    {
        bool exists = await Db.Languages.AnyAsync(l => l.TwoLetterCode == request.TwoLetterCode && !l.IsDeleted, cancellationToken);
        if (exists)
            return Result.Failure<LanguageSaveResult>(LanguageErrors.CodeAlreadyExists);

        if (request.IsDefault)
        {
            List<Language> previousDefaults = await Db.Languages.Where(x => x.IsDefault).ToListAsync(cancellationToken);
            foreach (Language previous in previousDefaults)
                previous.IsDefault = false;

            // See UpdateLanguageCommandHandler: the unique filtered index behind
            // "only one default language" is checked per statement, so the clear
            // above must actually land before the new row's own IsDefault=1 does.
            await Db.SaveChangesAsync(cancellationToken);
        }

        var lang = new Language
        {
            NameInNative = request.NameInNative,
            NameInEnglish = request.NameInEnglish,
            TwoLetterCode = request.TwoLetterCode,
            IsDefault = request.IsDefault,
            IsRtl = request.IsRtl,
            IsPublished = request.IsPublished,
            DisplayOrder = request.DisplayOrder,
            FlagIconFileId = request.FlagIconFileId,
        };

        Db.Languages.Add(lang);

        // A brand-new language has no pages of its own yet, but making it default
        // still changes which language EVERY EXISTING page's FullSlug omits the
        // prefix for — see PageHierarchy.RecomputeAllSlugsForDefaultLanguageChangeAsync.
        if (request.IsDefault)
            return Result.Success(await DefaultLanguageSwitch.ApplyAsync(Db, Sitemaps, request.TwoLetterCode, cancellationToken));

        return Result.Success(LanguageSaveResult.Unchanged);
    }
}
