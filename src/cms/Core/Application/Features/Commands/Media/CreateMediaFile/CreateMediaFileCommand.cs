using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;
using Application.Abstraction.Services.Files;

namespace Application.Features.Commands.Media.CreateMediaFile;

public sealed record CreateMediaFileCommand(FileUploadRequest Request) : ICommand<FileResult>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.MediaUpload;
}
