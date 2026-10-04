using Application.Abstraction.Data;
using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Languages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Languages.DeleteLanguage;

public sealed record DeleteLanguageCommand(int Id) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.LanguagesManage;
}
