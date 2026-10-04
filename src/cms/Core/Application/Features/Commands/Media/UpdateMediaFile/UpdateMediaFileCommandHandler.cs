using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Media;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Media.UpdateMediaFile;

internal sealed class UpdateMediaFileCommandHandler(ICmsApplicationDbContext Db) : ICommandHandler<UpdateMediaFileCommand>
{
    public async Task<Result> Handle(UpdateMediaFileCommand request, CancellationToken cancellationToken)
    {
        MediaFile? file = await Db.MediaFiles.FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (file is null)
            return Result.Failure(MediaFileErrors.NotFound);

        file.Title = request.Title;
        file.AltText = request.AltText;

        return Result.Success();
    }
}
