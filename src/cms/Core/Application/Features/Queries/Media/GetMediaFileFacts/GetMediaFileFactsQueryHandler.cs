using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Media.GetMediaFileFacts;

internal sealed record GetMediaFileFactsQueryHandler(ICmsApplicationDbContext Db)
    : IQueryHandler<GetMediaFileFactsQuery, MediaFileFacts?>
{
    public async Task<Result<MediaFileFacts?>> Handle(GetMediaFileFactsQuery request, CancellationToken cancellationToken)
    {
        string address = request.Address.Trim();
        if (address.Length == 0)
            return Result.Success<MediaFileFacts?>(null);

        // The library stores site-relative paths; a page may carry the full URL.
        string path = Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) ? uri.AbsolutePath : address;

        MediaFileFacts? facts = await Db.MediaFiles
            .AsNoTracking()
            .Where(m => m.FilePath == address || m.FilePath == path)
            .Select(m => new MediaFileFacts(m.Width, m.Height, m.MimeType, m.AltText))
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(facts);
    }
}
