namespace Application.Features.Queries.Languages.GetLanguageVisibilityImpact;

/// <param name="PublishedPages">Published pages of the language — what stops answering when it goes off the site.</param>
/// <param name="HiddenRedirects">Temporary redirects written when the language was last taken off the site.</param>
public sealed record LanguageVisibilityImpact(int PublishedPages, int HiddenRedirects);
