using Application.Abstraction.Data;
using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Languages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Languages.UpdateLanguage;

/// <param name="ConfirmHiding">
/// Required to take a visible language off the site (untick "On the site" or "In the
/// CMS"): every page of it stops answering, so the editor has to have seen the
/// warning. A save without it is refused rather than silently applied.
/// </param>
/// <param name="WhenHidden">What the language's addresses answer once it is off the site.</param>
/// <param name="RemoveHiddenRedirects">
/// When the language comes back: also remove the temporary redirects written when it
/// was taken off. Left alone they are harmless while it is live, and ready again if
/// it goes off a second time.
/// </param>
public sealed record UpdateLanguageCommand(
    int Id, string NameInNative, string NameInEnglish,
    string TwoLetterCode,
    int? FlagIconFileId, bool IsDefault, bool IsRtl, bool IsPublished,
    int DisplayOrder, bool IsActive,
    bool ConfirmHiding = false,
    LanguageHiddenHandling WhenHidden = LanguageHiddenHandling.NotFound,
    bool RemoveHiddenRedirects = false) : ICommand<LanguageSaveResult>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.LanguagesManage;
}

