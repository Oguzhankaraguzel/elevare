using Application.Abstraction.Data;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Pages;

/// <summary>
/// A homepage leaving Published — drafted, archived or deleted — takes its language's
/// root address down with it. That used to be refused outright, which left no way at
/// all to pull a language's homepage; it is allowed now, but only once the editor has
/// confirmed a warning. Shared by every command that can do it, so the editor's save,
/// the Pages list's status switch and delete all ask the same question.
/// </summary>
internal static class HomePageUnpublish
{
    /// <summary>
    /// The error asking for confirmation, or null when this change needs none. The
    /// default language's homepage gets a stronger one: it is the site's own "/".
    /// </summary>
    public static async Task<Error?> CheckAsync(
        ICmsApplicationDbContext db, PageInfo page, bool leavingPublished, bool confirmed, CancellationToken cancellationToken)
    {
        if (!leavingPublished || confirmed || page.ParentPageId is not null || page.Slug != "home")
            return null;

        bool isDefaultLanguage = await db.Languages
            .Where(l => l.Id == page.LanguageId)
            .Select(l => l.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        return isDefaultLanguage
            ? PageInfoErrors.DefaultHomePageUnpublishNeedsConfirmation
            : PageInfoErrors.HomePageUnpublishNeedsConfirmation;
    }
}
