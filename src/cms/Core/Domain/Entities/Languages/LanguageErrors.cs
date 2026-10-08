using SharedKernel.Concrete;

namespace Domain.Entities.Languages;

public static class LanguageErrors
{
    public static readonly Error NotFound = Error.NotFound("Language.NotFound", "The language was not found.");
    public static readonly Error CodeAlreadyExists = Error.Conflict("Language.CodeAlreadyExists", "A language with this code already exists.");
    public static readonly Error CannotDeleteDefaultLanguage = Error.Failure("Language.CannotDeleteDefaultLanguage", "The default language cannot be deleted.");
    public static readonly Error DefaultMustStayVisible = Error.Failure("Language.DefaultMustStayVisible", "The default language cannot be taken off the site or made inactive: the site's root address belongs to it. Make another language the default first, or turn on maintenance mode.");
    public static readonly Error HidingNeedsConfirmation = Error.Failure("Language.HidingNeedsConfirmation", "Taking a language off the site has to be confirmed, along with what its addresses should do.");
    public static readonly Error HasPages = Error.Conflict("Language.HasPages", "This language still has pages. To take it off the site, untick \"On the site\"; to delete it, delete its pages first.");
}
