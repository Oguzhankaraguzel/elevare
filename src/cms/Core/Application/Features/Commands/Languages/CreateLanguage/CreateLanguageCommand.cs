using Application.Abstraction.Data;
using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Languages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Languages.CreateLanguage;

public sealed record CreateLanguageCommand(
    string NameInNative, string NameInEnglish,
    string TwoLetterCode,
    int? FlagIconFileId, bool IsDefault, bool IsRtl, bool IsPublished,
    int DisplayOrder) : ICommand<LanguageSaveResult>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.LanguagesManage;
}
