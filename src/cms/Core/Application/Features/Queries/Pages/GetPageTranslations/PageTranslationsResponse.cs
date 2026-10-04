namespace Application.Features.Queries.Pages.GetPageTranslations;

public sealed record PageTranslationsResponse(
    int? PageGroupId,
    List<PageTranslationItem> Translations);
