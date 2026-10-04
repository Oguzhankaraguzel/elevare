using Domain.Entities.Media;

namespace Application.Features.Queries.Media.GetMediaFiles;

internal static class MediaFileSorting
{
    /// <summary>
    /// Applies the grid's chosen sort, always tie-broken by Id. Without that
    /// tie-break two files stored in the same instant can land on either side of a
    /// page boundary between requests, so a row shows up twice — or not at all —
    /// while paging. The default (newest first) also completes the
    /// (CreateDate, Id) tuple IX_MediaFiles_CreateDate_Id was built to serve.
    /// </summary>
    public static IQueryable<MediaFile> OrderBy(this IQueryable<MediaFile> query, GetMediaFilesQuery request) =>
        (request.SortBy, request.SortDescending) switch
        {
            ("FileName", true) => query.OrderByDescending(m => m.Title ?? m.FileName).ThenByDescending(m => m.Id),
            ("FileName", false) => query.OrderBy(m => m.Title ?? m.FileName).ThenBy(m => m.Id),
            ("FileSize", true) => query.OrderByDescending(m => m.FileSize).ThenByDescending(m => m.Id),
            ("FileSize", false) => query.OrderBy(m => m.FileSize).ThenBy(m => m.Id),
            ("MediaType", true) => query.OrderByDescending(m => m.MediaType).ThenByDescending(m => m.Id),
            ("MediaType", false) => query.OrderBy(m => m.MediaType).ThenBy(m => m.Id),
            ("CreateDate", false) => query.OrderBy(m => m.CreateDate).ThenBy(m => m.Id),
            _ => query.OrderByDescending(m => m.CreateDate).ThenByDescending(m => m.Id),
        };
}
