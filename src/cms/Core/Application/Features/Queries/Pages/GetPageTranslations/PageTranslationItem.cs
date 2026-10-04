using Domain.Entities.PageInfos;

namespace Application.Features.Queries.Pages.GetPageTranslations;

public sealed record PageTranslationItem(
    int PageId,
    int LanguageId,
    string TwoLetterCode,
    string LanguageName,
    string FullSlug,
    PageStatus Status);
