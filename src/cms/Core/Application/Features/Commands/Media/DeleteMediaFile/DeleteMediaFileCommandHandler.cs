using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Media;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Media.DeleteMediaFile;

internal sealed record DeleteMediaFileCommandHandler(ICmsApplicationDbContext Db) : ICommandHandler<DeleteMediaFileCommand>
{
    public async Task<Result> Handle(DeleteMediaFileCommand request, CancellationToken cancellationToken)
    {
        MediaFile? file = await Db.MediaFiles.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);
        if (file is null)
            return Result.Failure(MediaFileErrors.NotFound);

        Db.MediaFiles.Remove(file);

        return Result.Success();
    }
}
