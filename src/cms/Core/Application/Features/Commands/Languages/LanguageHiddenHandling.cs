namespace Application.Features.Commands.Languages;

/// <summary>
/// What the addresses of a language should answer once it is taken off the site
/// (unpublished or deactivated). Asked every time, because the right answer depends
/// on whether the language is expected to come back.
/// </summary>
public enum LanguageHiddenHandling
{
    /// <summary>The pages simply stop existing for visitors and search engines.</summary>
    NotFound = 0,

    /// <summary>
    /// Every page of the language gets a temporary (302) redirect to its counterpart
    /// in the default language, or to the default homepage when it has none — see
    /// <see cref="LanguageVisibilityRedirects"/>.
    /// </summary>
    RedirectToDefaultLanguage = 1,
}
