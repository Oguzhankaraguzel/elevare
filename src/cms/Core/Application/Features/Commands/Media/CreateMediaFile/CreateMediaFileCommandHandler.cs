using SharedKernel.Abstraction.Messaging;
using Application.Abstraction.Services.Files;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Media.CreateMediaFile;

internal sealed record CreateMediaFileCommandHandler(IFileService FileService) : ICommandHandler<CreateMediaFileCommand, FileResult>
{
    public async Task<Result<FileResult>> Handle(CreateMediaFileCommand request, CancellationToken cancellationToken)
    {
        Result<FileResult> result = await FileService.UploadAsync(request.Request, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<FileResult>(result.Error);

        return Result.Success(result.Value);
    }
}
