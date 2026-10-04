using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Media.GetMediaFileAltText;

internal sealed record GetMediaFileAltTextQueryHandler(ICmsApplicationDbContext Db)
    : IQueryHandler<GetMediaFileAltTextQuery, string?>
{
    public async Task<Result<string?>> Handle(GetMediaFileAltTextQuery request, CancellationToken cancellationToken)
    {
        string? altText = await Db.MediaFiles
            .AsNoTracking()
            .Where(m => m.FilePath == request.FilePath)
            .Select(m => m.AltText)
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(altText);
    }
}
