using SharedKernel.Concrete;

namespace Domain.Entities.PageInfos;

public static class PageInfoErrors
{
    public static readonly Error NotFound = Error.NotFound("PageInfo.NotFound", "The page was not found.");
    /// <summary>The page changed since the editor saving it was opened — see PageFingerprint.</summary>
    public static readonly Error EditedElsewhere = Error.Conflict("PageInfo.EditedElsewhere", "The page was changed by someone else since you opened it.");
    public static readonly Error SlugAlreadyExists = Error.Conflict("PageInfo.SlugAlreadyExists", "A page with this slug already exists for the selected language.");
    public static readonly Error CircularParentReference = Error.Failure("PageInfo.CircularParentReference", "A page cannot be set as its own parent or descendant.");
    public static readonly Error CannotDeleteWithChildren = Error.Conflict("PageInfo.CannotDeleteWithChildren", "The page cannot be deleted because it has child pages.");
    public static readonly Error CannotUnpublishHomePage = Error.Failure("PageInfo.CannotUnpublishHomePage", "The home page cannot be unpublished.");
    public static readonly Error SameLanguage = Error.Failure("PageInfo.SameLanguage", "The target language must be different from the source page's language.");
    public static readonly Error TranslationAlreadyExists = Error.Conflict("PageInfo.TranslationAlreadyExists", "A translation for this page already exists in the selected language.");
    public static readonly Error ParentNotFound = Error.NotFound("PageInfo.ParentNotFound", "The selected parent page was not found.");
    public static readonly Error ParentLanguageMismatch = Error.Failure("PageInfo.ParentLanguageMismatch", "The parent page must be in the same language.");
    public static readonly Error MaxDepthExceeded = Error.Failure("PageInfo.MaxDepthExceeded", "Pages can only be nested up to 3 levels deep.");
    public static readonly Error PreviewBaseUrlNotConfigured = Error.Failure("PageInfo.PreviewBaseUrlNotConfigured", "The public site's base URL is not configured yet — set it under Site Settings > Advanced > Public Site URL.");

    public static readonly Error InvalidSlug = Error.Failure("Page.InvalidSlug", "A valid slug could not be derived.");
    public static readonly Error CustomCodeNotAllowed = Error.Failure("Page.CustomCodeNotAllowed", "Only Developer, Admin or SuperAdmin can save raw script/custom-code content.");
}
