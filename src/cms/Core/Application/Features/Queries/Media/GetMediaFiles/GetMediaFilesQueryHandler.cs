using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Media;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Linq;
using SharedKernel.Extensions.Strings;

namespace Application.Features.Queries.Media.GetMediaFiles;

internal sealed record GetMediaFilesQueryHandler(ICmsApplicationDbContext Db) 
    : IQueryHandler<GetMediaFilesQuery, PagedResult<MediaFileResponse>>
{
    public async Task<Result<PagedResult<MediaFileResponse>>> Handle(GetMediaFilesQuery request, CancellationToken cancellationToken)
    {
        // Lower-cased both sides: LIKE is case-sensitive in PostgreSQL, so a search
        // for "logo" used to miss a file named "Logo.png". See SqlSearchProvider for
        // the full reasoning.
#pragma warning disable CA1304, CA1308, CA1311, CA1862
        string? search = request.Search?.ToLowerInvariant();

        // CA1304/CA1308/CA1311/CA1862 are suppressed, not obeyed: this is an EF Core expression tree,
        // not in-memory string work. The StringComparison overloads CA1862 recommends
        // cannot be translated to SQL (EF throws at runtime), and ToLowerInvariant —
        // CA1311's fix — has no SQL mapping either. Plain ToLower() is the ONLY form
        // that becomes lower() in the query, which is the whole point here.
        // CA1308 (prefer upper-casing) does not apply either: the comparison has to
        // agree with SQL lower(), so the term must be lower-cased to match.
        IQueryable<MediaFile> query = Db.MediaFiles.AsNoTracking()
                                                    // The original file name and the stored path are searched too: a
                                                    // library is most often searched by the name the file arrived with,
                                                    // or by pasting back part of a URL found in a page.
                                                    .WhereIf(!request.Search.IsNullOrWhiteSpace(), m => m.FileName.ToLower().Contains(search!)
                                                        || m.OriginalFileName.ToLower().Contains(search!)
                                                        || m.FilePath.ToLower().Contains(search!)
                                                        || m.Title != null && m.Title.ToLower().Contains(search!))
                                                    .WhereIf(request.MediaType.HasValue, m => m.MediaType == request.MediaType);
#pragma warning restore CA1304, CA1308, CA1311, CA1862

        int total = await query.CountAsync(cancellationToken);

        List<MediaFileResponse> items = await query
            .OrderBy(request)
            .Paginate(request.Page, request.PageSize)
            .Select(m => new MediaFileResponse(m.Id,
                                               m.FileName,
                                               m.OriginalFileName,
                                               m.FilePath,
                                               m.MimeType,
                                               m.FileSize,
                                               m.AltText,
                                               m.Title,
                                               m.MediaType,
                                               m.Width,
                                               m.Height,
                                               m.CreateDate,
                                               m.Renditions)
            )
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<MediaFileResponse>.Create(items, total, request.Page, request.PageSize));
    }
}
