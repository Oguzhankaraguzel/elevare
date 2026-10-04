using SharedKernel.Concrete;

namespace Domain.Entities.PageTemplates;

public static class PageTemplateErrors
{
    /// <summary>The template changed since the editor saving it was opened — see PageFingerprint.</summary>
    public static readonly Error EditedElsewhere = Error.Conflict("PageTemplate.EditedElsewhere", "The template was changed by someone else since you opened it.");
    public static readonly Error NotFound = Error.NotFound("PageTemplate.NotFound", "The template was not found.");

    public static readonly Error CustomCodeNotAllowed = Error.Failure("PageTemplate.CustomCodeNotAllowed", "Only Developer, Admin or SuperAdmin can save raw script/custom-code content.");
}
