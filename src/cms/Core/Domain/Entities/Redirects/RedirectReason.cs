namespace Domain.Entities.Redirects;

public enum RedirectReason
{
    Manual = 0,
    SlugChanged = 1,
    PageDeleted = 2,
    PageArchived = 3,

    /// <summary>
    /// Written when a language is taken off the site with "redirect to the default
    /// language" chosen: one temporary rule per page of that language, offered for
    /// removal when the language is published again.
    /// </summary>
    LanguageUnpublished = 4,
}
