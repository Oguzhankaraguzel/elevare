using Application.Abstraction.Data;
using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using Microsoft.EntityFrameworkCore;
using Domain.Entities.Media;

namespace Application.Features.Commands.Media.UpdateMediaFile;

public sealed record UpdateMediaFileCommand(int Id, string? Title, string? AltText) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.MediaUpload;
}
