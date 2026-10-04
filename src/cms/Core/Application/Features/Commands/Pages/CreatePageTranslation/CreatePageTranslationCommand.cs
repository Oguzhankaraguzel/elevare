using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Pages.CreatePageTranslation;

/// <summary>
/// Creates a new page in <see cref="TargetLanguageId"/> as a translation of
/// <see cref="SourcePageId"/>: title, SEO fields and GrapeJS content are cloned
/// from the source as a starting point, and both pages are linked via a shared
/// <c>PageGroup</c> (created if the source didn't already have one). The new
/// page always starts as Draft, regardless of the source page's status.
/// </summary>
/// <param name="ReplaceDeleted">
/// Set when the editor has been shown the trashed version of this translation and
/// chose to start fresh anyway. The tombstone is then purged rather than left
/// behind: once this new page takes the address, that row could never be restored,
/// so keeping it would only offer a Restore button that always fails.
/// </param>
public sealed record CreatePageTranslationCommand(
    int SourcePageId,
    int TargetLanguageId,
    bool ReplaceDeleted = false) : ICommand<int>, IRequirePermission
{
    // Creating a translation creates a brand-new page — same act, same permission as
    // CreatePage. Without this the command was reachable by any signed-in user, even
    // one with no page rights at all.
    public static string RequiredPermission => PermissionKeys.PagesCreate;
}
